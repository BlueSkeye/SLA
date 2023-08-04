using ghidra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A hash utility to uniquely identify a temporary Varnode in data-flow
    ///
    /// Most Varnodes can be identified within the data-flow graph by their storage address
    /// and the address of the PcodeOp that defines them.  For temporary registers,
    /// this does not work because the storage address is ephemeral. This class allows
    /// Varnodes like temporary registers (and constants) to be robustly identified
    /// by hashing details of the local data-flow.
    ///
    /// This class, when presented a Varnode via calcHash(), calculates a hash (getHash())
    /// and an address (getAddress()) of the PcodeOp most closely associated with the Varnode,
    /// either the defining op or the op directly reading the Varnode.
    /// There are actually four hash variants that can be calculated, labeled 0, 1, 2, or 3,
    /// which incrementally hash in a larger portion of data-flow.  The method uniqueHash() selects
    /// the simplest variant that causes the hash to be unique for the Varnode, among all
    /// the Varnodes that share the same address.
    ///
    /// The variant index is encoded in the hash, so the hash and the address are enough information
    /// to uniquely identify the Varnode. This is what is stored in the symbol table for
    /// a \e dynamic Symbol.
    internal class DynamicHash
    {
        public static uint[] transtable; ///< Translation of op-codes to hash values

        /// Number of Varnodes processed in the \b markvn list so far
        private uint vnproc;
        /// Number of PcodeOps processed in the \b markop list so far
        private uint opproc;
        /// Number of edges processed in the \b opedge list
        private uint opedgeproc;

        /// List of PcodeOps in the sub-graph being hashed
        private List<PcodeOp> markop;
        /// List of Varnodes is the sub-graph being hashed
        private List<Varnode> markvn;
        /// A staging area for Varnodes before formally adding to the sub-graph
        private List<Varnode> vnedge;
        /// The edges in the sub-graph
        private List<ToOpEdge> opedge;

        /// Address most closely associated with variable
        private Address addrresult;
        /// The calculated hash value
        private ulong hash;

        /// Add in the edge between the given Varnode and its defining PcodeOp
        /// When building the edge, certain p-code ops (CAST) are effectively ignored so that
        /// we get the same hash whether or not these ops are present.
        /// \param vn is the given Varnode
        private void buildVnUp(Varnode vn)
        {
            PcodeOp* op;
            for (; ; )
            {
                if (!vn.isWritten()) return;
                op = vn.getDef();
                if (transtable[op.code()] != 0) break; // Do not ignore this operation
                vn = op.getIn(0);
            }
            opedge.Add(ToOpEdge(op, -1));
        }

        /// Add in edges between the given Varnode and any PcodeOp that reads it
        /// When building edges, certain p-code ops (CAST) are effectively ignored so that
        /// we get the same hash whether or not these ops are present.
        /// \param vn is the given Varnode
        private void buildVnDown(Varnode vn)
        {
            int insize = opedge.Count;

            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp? op = iter.Current;
                Varnode? tmpvn = vn;
                while (transtable[(int)op.code()] == 0) {
                    tmpvn = op.getOut();
                    if (tmpvn == null) {
                        op = null;
                        break;
                    }
                    op = tmpvn.loneDescend();
                    if (op == null) break;
                }
                if (op == null) continue;
                int slot = op.getSlot(tmpvn);
                opedge.Add(new ToOpEdge(op, slot));
            }
            if ((uint)opedge.Count - insize > 1)
                opedge.Sort(insize, opedge.Count - insize, ToOpEdge.Comparer);
        }

        /// Move input Varnodes for the given PcodeOp into staging
        /// \param op is the given PcodeOp thats already in the sub-graph
        private void buildOpUp(PcodeOp op)
        {
            for (int i = 0; i < op.numInput(); ++i)
            {
                Varnode* vn = op.getIn(i);
                vnedge.Add(vn);
            }
        }

        /// Move the output Varnode for the given PcodeOp into staging
        /// \param op is the given PcodeOp thats already in the sub-graph
        private void buildOpDown(PcodeOp op)
        {
            Varnode* vn = op.getOut();
            if (vn == (Varnode)null) return;
            vnedge.Add(vn);
        }

        /// Move staged Varnodes into the sub-graph and mark them
        private void gatherUnmarkedVn()
        {
            for (int i = 0; i < vnedge.size(); ++i)
            {
                Varnode* vn = vnedge[i];
                if (vn.isMark()) continue;
                markvn.Add(vn);
                vn.setMark();
            }
            vnedge.clear();
        }

        /// Mark any new PcodeOps in the sub-graph
        private void gatherUnmarkedOp()
        {
            for (; opedgeproc < opedge.size(); ++opedgeproc)
            {
                PcodeOp* op = opedge[opedgeproc].getOp();
                if (op.isMark()) continue;
                markop.Add(op);
                op.setMark();
            }
        }

        /// Clean-up and piece together formal hash value
        /// Assume all the elements of the hash have been calculated.  Calculate the internal 32-bit hash
        /// based on these elements.  Construct the 64-bit hash by piecing together the 32-bit hash
        /// together with the core opcode, slot, and method.
        /// \param root is the Varnode to extract root characteristics from
        /// \param method is the method used to compute the hash elements
        private void pieceTogetherHash(Varnode root, uint method)
        {
            for (uint i = 0; i < markvn.size(); ++i) // Clear our marks
                markvn[i].clearMark();
            for (uint i = 0; i < markop.size(); ++i)
                markop[i].clearMark();

            if (opedge.size() == 0)
            {
                hash = (ulong)0;
                addrresult = Address();
                return;
            }

            uint reg = 0x3ba0fe06; // Calculate the 32-bit hash

            // Hash in information about the root
            reg = Globals.crc_update(reg, (uint)root.getSize());
            if (root.isConstant())
            {
                ulong val = root.getOffset();
                for (int i = 0; i < root.getSize(); ++i)
                {
                    reg = Globals.crc_update(reg, (uint)val);
                    val >>= 8;
                }
            }

            for (uint i = 0; i < opedge.size(); ++i)
                reg = opedge[i].hash(reg);

            // Build the final 64-bit hash
            PcodeOp* op = (PcodeOp)null;
            int slot = 0;
            uint ct;
            bool attachedop = true;
            for (ct = 0; ct < opedge.size(); ++ct)
            { // Find op that is directly attached to -root- i.e. not a skip op
                op = opedge[ct].getOp();
                slot = opedge[ct].getSlot();
                if ((slot < 0) && (op.getOut() == root)) break;
                if ((slot >= 0) && (op.getIn(slot) == root)) break;
            }
            if (ct == opedge.size())
            {   // If everything attached to the root was a skip op
                op = opedge[0].getOp(); // Return op that is not attached directly
                slot = opedge[0].getSlot();
                attachedop = false;
            }

            // 15 bits unused
            hash = attachedop ? 0 : 1;
            hash <<= 4;
            hash |= method;     // 4-bits
            hash <<= 7;
            hash |= (ulong)transtable[op.code()];  // 7-bits
            hash <<= 5;
            hash |= (ulong)(slot & 0x1f);   // 5-bits

            hash <<= 32;
            hash |= (ulong)reg;     // 32-bits for the neighborhood hash
            addrresult = op.getSeqNum().getAddr();
        }

        /// Convert given PcodeOp to a non-skip op by following data-flow
        /// For a DynamicHash on a PcodeOp, the op must not be a CAST or other skipped opcode.
        /// Test if the given op is a skip op, and if so follow data-flow indicated by the
        /// slot to another PcodeOp until we find one that isn't a skip op. Pass back the new PcodeOp
        /// and slot. Pass back null if the data-flow path ends.
        /// \param op is the given PcodeOp to modify
        /// \param slot is the slot to modify
        private static void moveOffSkip(PcodeOp op, int slot)
        {
            while (transtable[op.code()] == 0)
            {
                if (slot >= 0)
                {
                    Varnode* vn = op.getOut();
                    op = vn.loneDescend();
                    if (op == (PcodeOp)null)
                    {
                        return; // Indicate the end of the data-flow path
                    }
                    slot = op.getSlot(vn);
                }
                else
                {
                    Varnode* vn = op.getIn(0);
                    if (!vn.isWritten()) return;   // Indicate the end of the data-flow path
                    op = vn.getDef();
                }
            }
        }

        /// Remove any duplicate Varnodes in given list
        /// Otherwise preserve the order of the list.
        /// \param varlist is the given list of Varnodes to check
        private static void dedupVarnodes(List<Varnode> varlist)
        {
            if (varlist.size() < 2) return;
            List<Varnode*> resList;
            for (int i = 0; i < varlist.size(); ++i)
            {
                Varnode* vn = varlist[i];
                if (!vn.isMark())
                {
                    vn.setMark();
                    resList.Add(vn);
                }
            }
            for (int i = 0; i < resList.size(); ++i)
                resList[i].clearMark();
            varlist.swap(resList);
        }

        /// Called for each additional hash (after the first)
        public void clear()
        {
            markop.clear();
            markvn.clear();
            vnedge.clear();
            opedge.clear();
        }

        /// Calculate the hash for given Varnode and method
        /// A sub-graph is formed extending from the given Varnode as the root. The
        /// method specifies how the sub-graph is extended. In particular:
        ///  - Method 0 is extends to just immediate p-code ops reading or writing root
        ///  - Method 1 extends to one more level of inputs from method 0.
        ///  - Method 2 extends to one more level of outputs from method 0.
        ///  - Method 3 extends to inputs and outputs
        ///
        /// The resulting hash and address can be obtained after calling this method
        /// through getHash() and getAddress().
        /// \param root is the given root Varnode
        /// \param method is the hashing method to use: 0, 1, 2, 3
        public void calcHash(Varnode root, uint method)
        {
            vnproc = 0;
            opproc = 0;
            opedgeproc = 0;

            vnedge.Add(root);
            gatherUnmarkedVn();
            for (uint i = vnproc; i < markvn.size(); ++i)
                buildVnUp(markvn[i]);
            for (; vnproc < markvn.size(); ++vnproc)
                buildVnDown(markvn[vnproc]);

            switch (method)
            {
                case 0:
                    break;
                case 1:
                    gatherUnmarkedOp();
                    for (; opproc < markop.size(); ++opproc)
                        buildOpUp(markop[opproc]);

                    gatherUnmarkedVn();
                    for (; vnproc < markvn.size(); ++vnproc)
                        buildVnUp(markvn[vnproc]);
                    break;
                case 2:
                    gatherUnmarkedOp();
                    for (; opproc < markop.size(); ++opproc)
                        buildOpDown(markop[opproc]);

                    gatherUnmarkedVn();
                    for (; vnproc < markvn.size(); ++vnproc)
                        buildVnDown(markvn[vnproc]);
                    break;
                case 3:
                    gatherUnmarkedOp();
                    for (; opproc < markop.size(); ++opproc)
                        buildOpUp(markop[opproc]);

                    gatherUnmarkedVn();
                    for (; vnproc < markvn.size(); ++vnproc)
                        buildVnDown(markvn[vnproc]);
                    break;
                default:
                    break;
            }
            pieceTogetherHash(root, method);
        }

        /// Calculate hash for given PcodeOp, slot, and method
        public void calcHash(PcodeOp op, int slot, uint method)
        {
            Varnode* root;

            // slot may be from a hash unassociated with op
            // we need to check that slot indicates a valid Varnode
            if (slot < 0)
            {
                root = op.getOut();
                if (root == (Varnode)null) {
                    hash = 0;
                    addrresult = Address();
                    return;     // slot does not fit op
                }
            }
            else
            {
                if (slot >= op.numInput())
                {
                    hash = 0;
                    addrresult = Address();
                    return;     // slot does not fit op
                }
                root = op.getIn(slot);
            }
            vnproc = 0;
            opproc = 0;
            opedgeproc = 0;

            opedge.Add(ToOpEdge(op, slot));
            switch (method)
            {
                case 4:
                    break;
                case 5:
                    gatherUnmarkedOp();
                    for (; opproc < markop.size(); ++opproc)
                    {
                        buildOpUp(markop[opproc]);
                    }
                    gatherUnmarkedVn();
                    for (; vnproc < markvn.size(); ++vnproc)
                        buildVnUp(markvn[vnproc]);
                    break;
                case 6:
                    gatherUnmarkedOp();
                    for (; opproc < markop.size(); ++opproc)
                    {
                        buildOpDown(markop[opproc]);
                    }
                    gatherUnmarkedVn();
                    for (; vnproc < markvn.size(); ++vnproc)
                        buildVnDown(markvn[vnproc]);
                    break;
                default:
                    break;
            }
            pieceTogetherHash(root, method);
        }

        /// Select a unique hash for the given Varnode
        /// Collect the set of Varnodes at the same address as the given Varnode.
        /// Starting with method 0, increment the method and calculate hashes
        /// of the Varnodes until the given Varnode has a unique hash within the set.
        /// The resulting hash and address can be obtained after calling this method
        /// through getHash() and getAddress().
        ///
        /// In the rare situation that the last method still does not yield a unique hash,
        /// the hash encodes:
        ///   - the smallest number of hash collisions
        ///   - the method that produced the smallest number of hash collisions
        ///   - the position of the root within the collision list
        ///
        /// For most cases, this will still uniquely identify the root Varnode.
        /// \param root is the given root Varnode
        /// \param fd is the function (holding the data-flow graph)
        public void uniqueHash(Varnode root, Funcdata fd)
        {
            List<Varnode*> vnlist;
            List<Varnode*> vnlist2;
            List<Varnode*> champion;
            uint method;
            ulong tmphash;
            Address tmpaddr;
            uint maxduplicates = 8;

            for (method = 0; method < 4; ++method)
            {
                clear();
                calcHash(root, method);
                if (hash == 0) return;  // Can't get a good hash
                tmphash = hash;
                tmpaddr = addrresult;
                vnlist.clear();
                vnlist2.clear();
                gatherFirstLevelVars(vnlist, fd, tmpaddr, tmphash);
                for (uint i = 0; i < vnlist.size(); ++i)
                {
                    Varnode* tmpvn = vnlist[i];
                    clear();
                    calcHash(tmpvn, method);
                    if (getComparable(hash) == getComparable(tmphash))
                    {   // Hash collision
                        vnlist2.Add(tmpvn);
                        if (vnlist2.size() > maxduplicates) break;
                    }
                }
                if (vnlist2.size() <= maxduplicates)
                {
                    if ((champion.size() == 0) || (vnlist2.size() < champion.size()))
                    {
                        champion = vnlist2;
                        if (champion.size() == 1) break; // Current hash is unique
                    }
                }
            }
            if (champion.empty())
            {
                hash = (ulong)0;
                addrresult = Address(); // Couldn't find a unique hash
                return;
            }
            uint total = (uint)champion.size() - 1; // total is in range [0,maxduplicates-1]
            uint pos;
            for (pos = 0; pos <= total; ++pos)
                if (champion[pos] == root) break;
            if (pos > total)
            {
                hash = (ulong)0;
                addrresult = Address();
                return;
            }
            hash = tmphash | ((ulong)pos << 49); // Store three bits for position with list of duplicate hashes
            hash |= ((ulong)total << 52);   // Store three bits for total number of duplicate hashes
            addrresult = tmpaddr;
        }

        /// Select unique hash for given PcodeOp and slot
        /// Different hash methods are cycled through until a hash is found that distinguishes the given
        /// op from other PcodeOps at the same address. The final hash encoding and address of the PcodeOp are
        /// built for retrieval using getHash() and getAddress().
        /// \param op is the given PcodeOp
        /// \param slot is the particular slot to encode in the hash
        /// \param fd is the function containing the given PcodeOp
        public void uniqueHash(PcodeOp op, int slot, Funcdata fd)
        {
            List<PcodeOp*> oplist;
            List<PcodeOp*> oplist2;
            List<PcodeOp*> champion;
            uint method;
            ulong tmphash;
            Address tmpaddr;
            uint maxduplicates = 8;

            moveOffSkip(op, slot);
            if (op == (PcodeOp)null) {
                hash = (ulong)0;
                addrresult = Address(); // Hash cannot be calculated
                return;
            }
            gatherOpsAtAddress(oplist, fd, op.getAddr());
            for (method = 4; method < 7; ++method)
            {
                clear();
                calcHash(op, slot, method);
                if (hash == 0) return;  // Can't get a good hash
                tmphash = hash;
                tmpaddr = addrresult;
                oplist.clear();
                oplist2.clear();
                for (uint i = 0; i < oplist.size(); ++i)
                {
                    PcodeOp* tmpop = oplist[i];
                    if (slot >= tmpop.numInput()) continue;
                    clear();
                    calcHash(tmpop, slot, method);
                    if (getComparable(hash) == getComparable(tmphash))
                    {   // Hash collision
                        oplist2.Add(tmpop);
                        if (oplist2.size() > maxduplicates)
                            break;
                    }
                }
                if (oplist2.size() <= maxduplicates)
                {
                    if ((champion.size() == 0) || (oplist2.size() < champion.size()))
                    {
                        champion = oplist2;
                        if (champion.size() == 1)
                            break; // Current hash is unique
                    }
                }
            }
            if (champion.empty())
            {
                hash = (ulong)0;
                addrresult = Address(); // Couldn't find a unique hash
                return;
            }
            uint total = (uint)champion.size() - 1; // total is in range [0,maxduplicates-1]
            uint pos;
            for (pos = 0; pos <= total; ++pos)
                if (champion[pos] == op)
                    break;
            if (pos > total)
            {
                hash = (ulong)0;
                addrresult = Address();
                return;
            }
            hash = tmphash | ((ulong)pos << 49); // Store three bits for position with list of duplicate hashes
            hash |= ((ulong)total << 52);   // Store three bits for total number of duplicate hashes
            addrresult = tmpaddr;
        }

        /// \brief Given an address and hash, find the unique matching Varnode
        ///
        /// The method, number of collisions, and position are pulled out of the hash.
        /// Hashes for the method are performed at Varnodes linked to the given address,
        /// and the Varnode which matches the hash (and the position) is returned.
        /// If the number of collisions for the hash does not match, this method
        /// will not return a Varnode, even if the position looks valid.
        /// \param fd is the function containing the data-flow
        /// \param addr is the given address
        /// \param h is the hash
        /// \return the matching Varnode or NULL
        public Varnode findVarnode(Funcdata fd, Address addr, ulong h)
        {
            uint method = getMethodFromHash(h);
            uint total = getTotalFromHash(h);
            uint pos = getPositionFromHash(h);
            clearTotalPosition(h);
            List<Varnode*> vnlist;
            List<Varnode*> vnlist2;
            gatherFirstLevelVars(vnlist, fd, addr, h);
            for (uint i = 0; i < vnlist.size(); ++i)
            {
                Varnode* tmpvn = vnlist[i];
                clear();
                calcHash(tmpvn, method);
                if (getComparable(hash) == getComparable(h))
                    vnlist2.Add(tmpvn);
            }
            if (total != vnlist2.size()) return (Varnode)null;
            return vnlist2[pos];
        }

        /// \brief Given an address and hash, find the unique matching PcodeOp
        ///
        /// The method, slot, number of collisions, and position are pulled out of the hash.
        /// Hashes for the method are performed at PcodeOps linked to the given address,
        /// and the PcodeOp which matches the hash (and the position) is returned.
        /// If the number of collisions for the hash does not match, this method
        /// will not return a PcodeOp, even if the position looks valid.
        /// \param fd is the function containing the data-flow
        /// \param addr is the given address
        /// \param h is the hash
        /// \return the matching PcodeOp or NULL
        public PcodeOp findOp(Funcdata fd, Address addr, ulong h)
        {
            int method = getMethodFromHash(h);
            int slot = getSlotFromHash(h);
            int total = getTotalFromHash(h);
            int pos = getPositionFromHash(h);
            clearTotalPosition(h);
            List<PcodeOp*> oplist;
            List<PcodeOp*> oplist2;
            gatherOpsAtAddress(oplist, fd, addr);
            for (uint i = 0; i < oplist.size(); ++i)
            {
                PcodeOp* tmpop = oplist[i];
                if (slot >= tmpop.numInput()) continue;
                clear();
                calcHash(tmpop, slot, method);
                if (getComparable(hash) == getComparable(h))
                    oplist2.Add(tmpop);
            }
            if (total != oplist2.size())
                return (PcodeOp)null;
            return oplist2[pos];
        }

        /// Get the (current) hash
        public ulong getHash() => hash;

        /// Get the (current) address
        public Address getAddress() => addrresult;

        /// \brief Get the Varnodes immediately attached to PcodeOps at the given address
        ///
        /// Varnodes can be either inputs or outputs to the PcodeOps. The op-code, slot, and
        /// attachment boolean encoded in the hash are used to further filter the
        /// PcodeOp and Varnode objects. Varnodes are passed back in sequence with a list container.
        /// \param varlist is the container that will hold the matching Varnodes
        /// \param fd is the function holding the data-flow
        /// \param addr is the given address
        /// \param h is the given hash
        public static void gatherFirstLevelVars(List<Varnode> varlist, Funcdata fd,
            Address addr, ulong h)
        {
            uint opcVal = getOpCodeFromHash(h);
            int slot = getSlotFromHash(h);
            bool isnotattached = getIsNotAttached(h);
            PcodeOpTree::const_iterator iter = fd.beginOp(addr);
            PcodeOpTree::const_iterator enditer = fd.endOp(addr);

            while (iter != enditer)
            {
                PcodeOp* op = (*iter).second;
                ++iter;
                if (op.isDead()) continue;
                if (transtable[op.code()] != opcVal) continue;
                if (slot < 0)
                {
                    Varnode* vn = op.getOut();
                    if (vn != (Varnode)null)
                    {
                        if (isnotattached)
                        {   // If original varnode was not attached to (this) op
                            op = vn.loneDescend();
                            if (op != (PcodeOp)null)
                            {
                                if (transtable[op.code()] == 0)
                                { // Check for skipped op
                                    vn = op.getOut();
                                    if (vn == (Varnode)null) continue;
                                }
                            }
                        }
                        varlist.Add(vn);
                    }
                }
                else if (slot < op.numInput())
                {
                    Varnode* vn = op.getIn(slot);
                    if (isnotattached)
                    {
                        op = vn.getDef();
                        if ((op != (PcodeOp)null) && (transtable[op.code()] == 0))
                            vn = op.getIn(0);
                    }
                    varlist.Add(vn);
                }
            }
            dedupVarnodes(varlist);
        }

        /// \brief Place all PcodeOps at the given address in the provided container
        ///
        /// \param opList is the container to hold the PcodeOps
        /// \param fd is the function
        /// \param addr is the given address
        public static void gatherOpsAtAddress(List<PcodeOp> opList, Funcdata fd, Address addr)
        {
            PcodeOpTree::const_iterator iter, enditer;
            enditer = fd.endOp(addr);
            for (iter = fd.beginOp(addr); iter != enditer; ++iter)
            {
                PcodeOp* op = (*iter).second;
                if (op.isDead()) continue;
                opList.Add(op);
            }
        }

        /// Retrieve the encoded slot from a hash
        /// The hash encodes the input \e slot the root Varnode was attached to in its PcodeOp.
        /// \param h is the hash value
        /// \return the slot index or -1 if the Varnode was attached as output
        public static int getSlotFromHash(ulong h)
        {
            int res = (int)((h >> 32) & 0x1f);
            if (res == 31)
                res = -1;
            return res;
        }

        /// Retrieve the encoded method from a hash
        /// The hash encodes the \e method used to produce it.
        /// \param h is the hash value
        /// \return the method: 0, 1, 2, 3
        public static uint getMethodFromHash(ulong h)
        {
            return (uint)((h >> 44) & 0xf);
        }

        /// Retrieve the encoded op-code from a hash
        /// The hash encodes the op-code of the p-code op attached to the root Varnode
        /// \param h is the hash value
        /// \return the op-code as an integer
        public static uint getOpCodeFromHash(ulong h)
        {
            return (h >> 37) & 0x7f;
        }

        /// Retrieve the encoded position from a hash
        /// The hash encodes the position of the root Varnode within the list of hash collisions
        /// \param h is the hash value
        /// \return the position of the root
        public static uint getPositionFromHash(ulong h)
        {
            return (uint)((h >> 49) & 7);
        }

        /// Retrieve the encoded collision total from a hash
        /// The hash encodes the total number of collisions for that hash
        /// \param h is the hash value
        /// \return the total number of collisions
        public static uint getTotalFromHash(ulong h)
        {
            return ((uint)((h >> 52) & 7) + 1);
        }

        /// Retrieve the attachment boolean from a hash
        /// The hash encodes whether or not the root was directly attached to its PcodeOp
        /// \param h is the hash value
        /// \return \b true if the root was not attached
        public static bool getIsNotAttached(ulong h)
        {
            return (((h >> 48) & 1) != 0);
        }

        /// Clear the collision total and position fields within a hash
        /// The position and total collisions fields are set by the uniqueness and
        /// need to be cleared when comparing raw hashes.
        /// \param h is a reference to the hash to modify
        public static void clearTotalPosition(ulong h)
        {
            ulong val = 0x3f;
            val <<= 49;
            val = ~val;
            h &= val;
        }

        /// Get only the formal hash for comparing
        public static uint getComparable(ulong h)
        {
            return (uint)h;
        }
    }
}
