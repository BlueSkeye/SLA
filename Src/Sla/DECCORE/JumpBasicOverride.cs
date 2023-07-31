using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A basic jump-table model incorporating manual override information
    ///
    /// The list of potential target addresses produced by the BRANCHIND is not recovered by \b this
    /// model, but must provided explicitly via setAddresses().
    /// The model tries to repurpose some of the analysis that JumpBasic does to recover the switch variable.
    /// But it will revert to the trivial model if it can't find a suitable switch variable.
    internal class JumpBasicOverride : JumpBasic
    {
        /// Absolute address table (manually specified)
        private set<Address> adset;
        /// Normalized switch variable values associated with addresses
        private List<ulong> values;
        /// Address associated with each value
        private List<Address> addrtable;
        /// Possible start for guessing values that match addresses
        private ulong startingvalue;
        /// Dynamic info for recovering normalized switch variable
        private Address normaddress;
        /// if (hash==0) there is no normalized switch (use trivial model)
        private ulong hash;
        /// \b true if we use a trivial value model
        private bool istrivial;

        /// \brief Return the PcodeOp (within the PathMeld set) that takes the given Varnode as input
        ///
        /// If there no PcodeOp in the set reading the Varnode, null is returned
        /// \param vn is the given Varnode
        /// \return the PcodeOp or null
        private int findStartOp(Varnode vn)
        {
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = vn.beginDescend();
            enditer = vn.endDescend();
            for (; iter != enditer; ++iter)
                (*iter).setMark();
            int res = -1;
            for (int i = 0; i < pathMeld.numOps(); ++i)
            {
                if (pathMeld.getOp(i).isMark())
                {
                    res = i;
                    break;
                }
            }
            for (iter = vn.beginDescend(); iter != enditer; ++iter)
                (*iter).clearMark();
            return res;
        }

        /// \brief Test a given Varnode as a potential normalized switch variable
        ///
        /// This method tries to figure out the set of values for the Varnode that
        /// produce the manually provided set of addresses.   Starting with \e startingvalue
        /// and simply incrementing by one to obtain new values, the path from the potential variable
        /// to the BRANCHIND is emulated to produce addresses in the manual set.  Duplicates and
        /// misses are allowed. Once we see all addresses in the manual set,
        /// the method returns the index of the starting op, otherwise -1 is returned.
        /// \param fd is the function containing the switch
        /// \param trialvn is the given trial normalized switch variable
        /// \param tolerance is the number of misses that will be tolerated
        /// \return the index of the starting PcodeOp within the PathMeld or -1
        private int trialNorm(Funcdata fd, Varnode trialvn, uint tolerance)
        {
            int opi = findStartOp(trialvn);
            if (opi < 0) return -1;
            PcodeOp* startop = pathMeld.getOp(opi);

            if (!values.empty())        // Have we already worked out the values and addresses
                return opi;

            EmulateFunction emul(fd);
            //  if (loadpoints != (List<LoadTable> *)0)
            //    emul.setLoadCollect(true);

            AddrSpace* spc = startop.getAddr().getSpace();
            ulong val = startingvalue;
            ulong addr;
            uint total = 0;
            uint miss = 0;
            set<Address> alreadyseen;
            while (total < adset.size())
            {
                try
                {
                    addr = emul.emulatePath(val, pathMeld, startop, trialvn);
                }
                catch (LowlevelError err) { // Something went wrong with emulation
                    addr = 0;
                    miss = tolerance;       // Terminate early
                }
                addr = AddrSpace::addressToByte(addr, spc.getWordSize());
                Address newaddr(spc, addr);
                if (adset.find(newaddr) != adset.end())
                {
                    if (alreadyseen.insert(newaddr).second) // If this is the first time we've seen this address
                        total += 1;     // Count it
                    values.Add(val);
                    addrtable.Add(newaddr);
                    // We may be seeing the same (valid) address over and over, without seeing others in -adset-
                    // Terminate if things get too large
                    if (values.size() > adset.size() + 100) break;
                    miss = 0;
                }
                else
                {
                    miss += 1;
                    if (miss >= tolerance) break;
                }
                val += 1;
            }

            //  if ((loadpoint != (List<LoadTable> *)0)&&(total == adset.size()))
            //    emul.collectLoadPoints(*loadpoints);
            if (total == adset.size())
                return opi;
            values.clear();
            addrtable.clear();
            return -1;
        }

        /// \brief Convert \b this to a trivial model
        ///
        /// Since we have an absolute set of addresses, if all else fails we can use the indirect variable
        /// as the normalized switch and the addresses as the values, similar to JumpModelTrivial
        private void setupTrivial()
        {
            set<Address>::const_iterator iter;
            if (addrtable.empty())
            {
                for (iter = adset.begin(); iter != adset.end(); ++iter)
                {
                    Address addr = *iter;
                    addrtable.Add(addr);
                }
            }
            values.clear();
            for (int i = 0; i < addrtable.size(); ++i)
                values.Add(addrtable[i].getOffset());
            varnodeIndex = 0;
            normalvn = pathMeld.getVarnode(0);
            istrivial = true;
        }

        /// \brief Find a potential normalized switch variable
        ///
        /// This method is called if the normalized switch variable is not explicitly provided.
        /// It looks for the normalized Varnode in the most common jump-table constructions,
        /// otherwise it returns null.
        /// \return the potential normalized switch variable or null
        private Varnode findLikelyNorm()
        {
            Varnode* res = (Varnode)null;
            PcodeOp* op;
            uint i;

            for (i = 0; i < pathMeld.numOps(); ++i)
            { // Look for last LOAD
                op = pathMeld.getOp(i);
                if (op.code() == OpCode.CPUI_LOAD)
                {
                    res = pathMeld.getOpParent(i);
                    break;
                }
            }
            if (res == (Varnode)null) return res;
            i += 1;
            while (i < pathMeld.numOps())
            { // Look for preceding ADD
                op = pathMeld.getOp(i);
                if (op.code() == OpCode.CPUI_INT_ADD)
                {
                    res = pathMeld.getOpParent(i);
                    break;
                }
                ++i;
            }
            i += 1;
            while (i < pathMeld.numOps())
            { // Look for preceding MULT
                op = pathMeld.getOp(i);
                if (op.code() == OpCode.CPUI_INT_MULT)
                {
                    res = pathMeld.getOpParent(i);
                    break;
                }
                ++i;
            }
            return res;
        }

        /// \brief Clear varnodes and ops that are specific to one instance of a function
        private void clearCopySpecific()
        {
            selectguards.clear();
            pathMeld.clear();
            normalvn = (Varnode)null;
            switchvn = (Varnode)null;
        }

        /// \param jt is the parent JumpTable
        public JumpBasicOverride(JumpTable jt)
            : base(jt)
        {
            startingvalue = 0;
            hash = 0;
            istrivial = false;
        }

        /// Manually set the address table for \b this model
        /// \param adtable is the list of externally provided addresses, which will be deduped
        public void setAddresses(List<Address> adtable)
        {
            for (int i = 0; i < adtable.size(); ++i)
                adset.insert(adtable[i]);
        }

        public void setNorm(Address addr, ulong h)
        {
            normaddress = addr;
            hash = h;
        }   ///< Set the normalized switch variable

        public void setStartingValue(ulong val)
        {
            startingvalue = val;
        }       ///< Set the starting value for the normalized range

        public override bool isOverride() => true;

        public override int getTableSize() => addrtable.size();

        public override bool recoverModel(Funcdata fd, PcodeOp indop, uint matchsize,
            uint maxtablesize)
        {
            clearCopySpecific();
            findDeterminingVarnodes(indop, 0);
            if (!istrivial)
            {       // If we haven't previously decided to use trivial model
                Varnode* trialvn = (Varnode)null;
                if (hash != 0)
                {
                    DynamicHash dyn;
                    trialvn = dyn.findVarnode(fd, normaddress, hash);
                }
                // If there was never a specified norm, or the specified norm was never recovered
                if ((trialvn == (Varnode)null) && (values.empty() || (hash == 0)))
                    trialvn = findLikelyNorm();

                if (trialvn != (Varnode)null)
                {
                    int opi = trialNorm(fd, trialvn, 10);
                    if (opi >= 0)
                    {
                        varnodeIndex = opi;
                        normalvn = trialvn;
                        return true;
                    }
                }
            }
            setupTrivial();
            return true;
        }

        public override void buildAddresses(Funcdata fd, PcodeOp indop, List<Address> addresstable,
            List<LoadTable> loadpoints)
        {
            addresstable = addrtable;   // Addresses are already calculated, just copy them out
        }

        // findUnnormalized inherited from JumpBasic
        public override void buildLabels(Funcdata fd, List<Address> addresstable,
            List<ulong> label, JumpModel orig)
        {
            ulong addr;

            for (uint i = 0; i < values.size(); ++i)
            {
                try
                {
                    addr = backup2Switch(fd, values[i], normalvn, switchvn);
                }
                catch (EvaluationError err) {
                    addr = 0xBAD1ABE1;
                }
                label.Add(addr);
                if (label.size() >= addresstable.size()) break; // This should never happen
            }

            while (label.size() < addresstable.size()) {
                fd.warning("Bad switch case", addresstable[label.size()]); // This should never happen
                label.Add(0xBAD1ABE1);
            }
        }

        // foldInNormalization inherited from JumpBasic
        public override bool foldInGuards(Funcdata fd, JumpTable jump) => false;

        public override bool sanityCheck(Funcdata fd, PcodeOp indop, List<Address> addresstable)
            => true;

        public override JumpModel clone(JumpTable jt)
        {
            JumpBasicOverride* res = new JumpBasicOverride(jt);
            res.adset = adset;
            res.values = values;
            res.addrtable = addrtable;
            res.startingvalue = startingvalue;
            res.normaddress = normaddress;
            res.hash = hash;
            return res;
        }

        public override void clear()
        {
            // -adset- is a permanent feature, do no clear
            // -startingvalue- is permanent
            // -normaddress- is permanent
            // -hash- is permanent
            values.clear();
            addrtable.clear();
            istrivial = false;
        }

        public override void encode(Encoder encoder)
        {
            set<Address>::const_iterator iter;

            encoder.openElement(ELEM_BASICOVERRIDE);
            for (iter = adset.begin(); iter != adset.end(); ++iter)
            {
                encoder.openElement(ELEM_DEST);
                AddrSpace* spc = (*iter).getSpace();
                ulong off = (*iter).getOffset();
                spc.encodeAttributes(encoder, off);
                encoder.closeElement(ELEM_DEST);
            }
            if (hash != 0)
            {
                encoder.openElement(ELEM_NORMADDR);
                normaddress.getSpace().encodeAttributes(encoder, normaddress.getOffset());
                encoder.closeElement(ELEM_NORMADDR);
                encoder.openElement(ELEM_NORMHASH);
                encoder.writeUnsignedInteger(ATTRIB_CONTENT, hash);
                encoder.closeElement(ELEM_NORMHASH);
            }
            if (startingvalue != 0)
            {
                encoder.openElement(ELEM_STARTVAL);
                encoder.writeUnsignedInteger(ATTRIB_CONTENT, startingvalue);
                encoder.closeElement(ELEM_STARTVAL);
            }
            encoder.closeElement(ELEM_BASICOVERRIDE);
        }

        public override void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_BASICOVERRIDE);
            for (; ; )
            {
                uint subId = decoder.openElement();
                if (subId == 0) break;
                if (subId == ELEM_DEST)
                {
                    VarnodeData vData;
                    vData.decodeFromAttributes(decoder);
                    adset.insert(vData.getAddr());
                }
                else if (subId == ELEM_NORMADDR)
                {
                    VarnodeData vData;
                    vData.decodeFromAttributes(decoder);
                    normaddress = vData.getAddr();
                }
                else if (subId == ELEM_NORMHASH)
                {
                    hash = decoder.readUnsignedInteger(ATTRIB_CONTENT);
                }
                else if (subId == ELEM_STARTVAL)
                {
                    startingvalue = decoder.readUnsignedInteger(ATTRIB_CONTENT);
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
            if (adset.empty())
                throw new LowlevelError("Empty jumptable override");
        }
    }
}
