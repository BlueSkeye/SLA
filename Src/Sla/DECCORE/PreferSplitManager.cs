using Sla.CORE;

using VarnodeLocSet = System.Collections.Generic.HashSet<Sla.DECCORE.Varnode>; // VarnodeCompareLocDef : A set of Varnodes sorted by location (then by definition)

namespace Sla.DECCORE
{
    internal class PreferSplitManager
    {
        internal class SplitInstance
        {
            // friend class PreferSplitManager;
            internal int splitoffset;
            internal Varnode vn;
            // Most significant piece
            internal Varnode? hi;
            // Least significant piece
            internal Varnode? lo;
            
            public SplitInstance(Varnode v, int off)
            {
                vn = v;
                splitoffset = off;
                hi = (Varnode)null;
                lo = (Varnode)null;
            }
        }
        
        private Funcdata data;
        private List<PreferSplitRecord> records;
        // Copies of temporaries that need additional splitting
        private List<PcodeOp> tempsplits;

        private void fillinInstance(SplitInstance inst, bool bigendian, bool sethi, bool setlo)
        {
            // Define the varnode pieces of -inst-
            Varnode vn = inst.vn;
            int losize;
            if (bigendian)
                losize = vn.getSize() - inst.splitoffset;
            else
                losize = inst.splitoffset;
            int hisize = vn.getSize() - losize;
            if (vn.isConstant()) {
                ulong origval = vn.getOffset();

                ulong loval = origval & Globals.calc_mask((uint)losize);// Split the constant into two pieces
                ulong hival = (origval >> 8 * losize) & Globals.calc_mask((uint)hisize);
                if (setlo && (inst.lo == (Varnode)null))
                    inst.lo = data.newConstant(losize, loval);
                if (sethi && (inst.hi == (Varnode)null))
                    inst.hi = data.newConstant(hisize, hival);
            }
            else {
                if (bigendian) {
                    if (setlo && (inst.lo == (Varnode)null))
                        inst.lo = data.newVarnode(losize, vn.getAddr() + inst.splitoffset);
                    if (sethi && (inst.hi == (Varnode)null))
                        inst.hi = data.newVarnode(hisize, vn.getAddr());
                }
                else {
                    if (setlo && (inst.lo == (Varnode)null))
                        inst.lo = data.newVarnode(losize, vn.getAddr());
                    if (sethi && (inst.hi == (Varnode)null))
                        inst.hi = data.newVarnode(hisize, vn.getAddr() + inst.splitoffset);
                }
            }
        }

        private void createCopyOps(SplitInstance ininst, SplitInstance outinst, PcodeOp op, bool istemp)
        {
            // Create COPY ops based on input -ininst- and output -outinst- to replace -op-
            PcodeOp hiop = data.newOp(1, op.getAddr()); // Create two new COPYs
            PcodeOp loop = data.newOp(1, op.getAddr());
            data.opSetOpcode(hiop, OpCode.CPUI_COPY);
            data.opSetOpcode(loop, OpCode.CPUI_COPY);

            data.opInsertAfter(loop, op); // Insert new COPYs at same position as original operation
            data.opInsertAfter(hiop, op);
            data.opUnsetInput(op, 0);  // Unset input so we can reassign free inputs to new ops

            data.opSetOutput(hiop, outinst.hi); // Outputs
            data.opSetOutput(loop, outinst.lo);
            data.opSetInput(hiop, ininst.hi, 0);
            data.opSetInput(loop, ininst.lo, 0);
            tempsplits.Add(hiop);
            tempsplits.Add(loop);
        }

        private bool testDefiningCopy(SplitInstance inst, PcodeOp def, out bool istemp)
        {
            // Check that -inst- defined by -def- is really splittable
            Varnode invn = def.getIn(0);
            istemp = false;
            if (!invn.isConstant()) {
                if (invn.getSpace().getType() != spacetype.IPTR_INTERNAL) {
                    PreferSplitRecord? inrec = findRecord(invn);
                    if (inrec == (PreferSplitRecord)null) return false;
                    if (inrec.splitoffset != inst.splitoffset) return false;
                    if (!invn.isFree()) return false;
                }
                else
                    istemp = true;
            }
            return true;
        }

        private void splitDefiningCopy(SplitInstance inst, PcodeOp def, bool istemp)
        {
            // Do split of prefered split varnode that is defined by a COPY
            Varnode invn = def.getIn(0);
            SplitInstance ininst = new SplitInstance(invn, inst.splitoffset);
            bool bigendian = inst.vn.getSpace().isBigEndian();
            fillinInstance(inst, bigendian, true, true);
            fillinInstance(ininst, bigendian, true, true);
            createCopyOps(ininst, inst, def, istemp);
        }

        private bool testReadingCopy(SplitInstance inst, PcodeOp readop, out bool istemp)
        { // Check that -inst- read by -readop- is really splittable
            Varnode outvn = readop.getOut();
            istemp = false;
            if (outvn.getSpace().getType() != spacetype.IPTR_INTERNAL) {
                PreferSplitRecord? outrec = findRecord(outvn);
                if (outrec == (PreferSplitRecord)null) return false;
                if (outrec.splitoffset != inst.splitoffset) return false;
            }
            else
                istemp = true;
            return true;
        }

        private void splitReadingCopy(SplitInstance inst, PcodeOp readop, bool istemp)
        {
            // Do split of varnode that is read by a COPY
            Varnode outvn = readop.getOut();
            SplitInstance outinst = new SplitInstance(outvn, inst.splitoffset);
            bool bigendian = inst.vn.getSpace().isBigEndian();
            fillinInstance(inst, bigendian, true, true);
            fillinInstance(outinst, bigendian, true, true);
            createCopyOps(inst, outinst, readop, istemp);
        }

        private bool testZext(SplitInstance inst, PcodeOp op)
        {
            // Check that -inst- defined by ZEXT is really splittable
            Varnode invn = op.getIn(0);
            if (invn.isConstant())
                return true;
            bool bigendian = inst.vn.getSpace().isBigEndian();
            int losize;
            if (bigendian)
                losize = inst.vn.getSize() - inst.splitoffset;
            else
                losize = inst.splitoffset;
            if (invn.getSize() != losize) return false;
            return true;
        }

        private void splitZext(SplitInstance inst, PcodeOp op)
        {
            SplitInstance ininst = new SplitInstance(op.getIn(0),inst.splitoffset);
            int losize, hisize;
            bool bigendian = inst.vn.getSpace().isBigEndian();
            if (bigendian) {
                hisize = inst.splitoffset;
                losize = inst.vn.getSize() - inst.splitoffset;
            }
            else {
                losize = inst.splitoffset;
                hisize = inst.vn.getSize() - inst.splitoffset;
            }
            if (ininst.vn.isConstant()) {
                ulong origval = ininst.vn.getOffset();
                ulong loval = origval & Globals.calc_mask((uint)losize);// Split the constant into two pieces
                ulong hival = (origval >> 8 * losize) & Globals.calc_mask((uint)hisize);
                ininst.lo = data.newConstant(losize, loval);
                ininst.hi = data.newConstant(hisize, hival);
            }
            else {
                ininst.lo = ininst.vn;
                ininst.hi = data.newConstant(hisize, 0);
            }

            fillinInstance(inst, bigendian, true, true);
            createCopyOps(ininst, inst, op, false);
        }

        private bool testPiece(SplitInstance inst, PcodeOp op)
        {
            // Check that -inst- defined by PIECE is really splittable
            if (inst.vn.getSpace().isBigEndian()) {
                if (op.getIn(0).getSize() != inst.splitoffset) return false;
            }
            else {
                if (op.getIn(1).getSize() != inst.splitoffset) return false;
            }
            return true;
        }

        private void splitPiece(SplitInstance inst, PcodeOp op)
        {
            Varnode loin = op.getIn(1);
            Varnode hiin = op.getIn(0);
            bool bigendian = inst.vn.getSpace().isBigEndian();
            fillinInstance(inst, bigendian, true, true);
            PcodeOp hiop = data.newOp(1, op.getAddr());
            PcodeOp loop = data.newOp(1, op.getAddr());
            data.opSetOpcode(hiop, OpCode.CPUI_COPY);
            data.opSetOpcode(loop, OpCode.CPUI_COPY);
            data.opSetOutput(hiop, inst.hi); // Outputs are the pieces of the original
            data.opSetOutput(loop, inst.lo);

            data.opInsertAfter(loop, op);
            data.opInsertAfter(hiop, op);
            data.opUnsetInput(op, 0);
            data.opUnsetInput(op, 1);

            if (hiin.isConstant())
                hiin = data.newConstant(hiin.getSize(), hiin.getOffset());
            data.opSetInput(hiop, hiin, 0);    // Input for the COPY of the most significant part comes from high part of PIECE
            if (loin.isConstant())
                loin = data.newConstant(loin.getSize(), loin.getOffset());
            data.opSetInput(loop, loin, 0);    // Input for the COPY of the least significant part comes from low part of PIECE
        }

        private bool testSubpiece(SplitInstance inst, PcodeOp op)
        { // Check that -inst- read by SUBPIECE is really splittable
            Varnode vn = inst.vn;
            Varnode outvn = op.getOut();
            int suboff = (int)op.getIn(1).getOffset();
            if (suboff == 0) {
                if (vn.getSize() - inst.splitoffset != outvn.getSize())
                    return false;
            }
            else {
                if (vn.getSize() - suboff != inst.splitoffset)
                    return false;
                if (outvn.getSize() != inst.splitoffset)
                    return false;
            }
            return true;
        }

        private void splitSubpiece(SplitInstance inst, PcodeOp op)
        {
            // Knowing -op- is a OpCode.CPUI_SUBPIECE that extracts a logical piece from -inst-, rewrite it to a copy
            Varnode vn = inst.vn;
            int suboff = (int)op.getIn(1).getOffset();
            bool grabbinglo = (suboff == 0);

            bool bigendian = vn.getSpace().isBigEndian();
            fillinInstance(inst, bigendian, !grabbinglo, grabbinglo);
            data.opSetOpcode(op, OpCode.CPUI_COPY); // Change SUBPIECE to a copy
            data.opRemoveInput(op, 1);

            // Input is most/least significant piece, depending on which the SUBPIECE extracts
            Varnode invn = grabbinglo ? inst.lo : inst.hi;
            data.opSetInput(op, invn, 0);
        }

        private bool testLoad(SplitInstance inst, PcodeOp op)
        {
            return true;
        }

        private void splitLoad(SplitInstance inst, PcodeOp op)
        {
            // Knowing -op- is a OpCode.CPUI_LOAD that defines the -inst- varnode, split it into two pieces
            bool bigendian = inst.vn.getSpace().isBigEndian();
            fillinInstance(inst, bigendian, true, true);
            PcodeOp hiop = data.newOp(2, op.getAddr());  // Create two new LOAD ops
            PcodeOp loop = data.newOp(2, op.getAddr());
            PcodeOp addop = data.newOp(2, op.getAddr());
            Varnode ptrvn = op.getIn(1);

            data.opSetOpcode(hiop, OpCode.CPUI_LOAD);
            data.opSetOpcode(loop, OpCode.CPUI_LOAD);

            data.opSetOpcode(addop, OpCode.CPUI_INT_ADD); // Create a new ADD op to calculate and hold the second pointer

            data.opInsertAfter(loop, op);
            data.opInsertAfter(hiop, op);
            data.opInsertAfter(addop, op);
            data.opUnsetInput(op, 1);  // Free up ptrvn

            Varnode addvn = data.newUniqueOut(ptrvn.getSize(), addop);
            data.opSetInput(addop, ptrvn, 0);
            data.opSetInput(addop, data.newConstant(ptrvn.getSize(), (ulong)inst.splitoffset), 1);

            data.opSetOutput(hiop, inst.hi); // Outputs are the pieces of the original
            data.opSetOutput(loop, inst.lo);
            Varnode spaceid = op.getIn(0);
            AddrSpace spc = spaceid.getSpaceFromConst();
            spaceid = data.newConstant(spaceid.getSize(), spaceid.getOffset()); // Duplicate original spaceid into new LOADs
            data.opSetInput(hiop, spaceid, 0);
            spaceid = data.newConstant(spaceid.getSize(), spaceid.getOffset());
            data.opSetInput(loop, spaceid, 0);
            if (ptrvn.isFree())        // Don't read a free varnode twice
                ptrvn = data.newVarnode(ptrvn.getSize(), ptrvn.getSpace(), ptrvn.getOffset());

            if (spc.isBigEndian()) {
                data.opSetInput(hiop, ptrvn, 1);
                data.opSetInput(loop, addvn, 1);
            }
            else {
                data.opSetInput(hiop, addvn, 1);
                data.opSetInput(loop, ptrvn, 1);
            }
        }

        private bool testStore(SplitInstance inst, PcodeOp op)
        {
            return true;
        }

        private void splitStore(SplitInstance inst, PcodeOp op)
        {
            // Knowing -op- stores the value -inst-, split it in two
            fillinInstance(inst, inst.vn.getSpace().isBigEndian(), true, true);
            PcodeOp hiop = data.newOp(3, op.getAddr());  // Create 2 new STOREs
            PcodeOp loop = data.newOp(3, op.getAddr());
            PcodeOp addop = data.newOp(2, op.getAddr());
            Varnode ptrvn = op.getIn(1);

            data.opSetOpcode(hiop, OpCode.CPUI_STORE);
            data.opSetOpcode(loop, OpCode.CPUI_STORE);

            data.opSetOpcode(addop, OpCode.CPUI_INT_ADD); // Create a new ADD op to calculate and hold the second pointer

            data.opInsertAfter(loop, op);
            data.opInsertAfter(hiop, op);
            data.opInsertAfter(addop, op);
            data.opUnsetInput(op, 1);  // Free up ptrvn
            data.opUnsetInput(op, 2);  // Free up inst

            Varnode addvn = data.newUniqueOut(ptrvn.getSize(), addop);
            data.opSetInput(addop, ptrvn, 0);
            data.opSetInput(addop, data.newConstant(ptrvn.getSize(), (ulong)inst.splitoffset), 1);

            data.opSetInput(hiop, inst.hi, 2); // Varnodes "being stored" are the pieces of the original
            data.opSetInput(loop, inst.lo, 2);
            Varnode spaceid = op.getIn(0);
            AddrSpace spc = spaceid.getSpaceFromConst();
            spaceid = data.newConstant(spaceid.getSize(), spaceid.getOffset()); // Duplicate original spaceid into new STOREs
            data.opSetInput(hiop, spaceid, 0);
            spaceid = data.newConstant(spaceid.getSize(), spaceid.getOffset());
            data.opSetInput(loop, spaceid, 0);

            if (ptrvn.isFree())        // Don't read a free varnode twice
                ptrvn = data.newVarnode(ptrvn.getSize(), ptrvn.getSpace(), ptrvn.getOffset());
            if (spc.isBigEndian()) {
                data.opSetInput(hiop, ptrvn, 1);
                data.opSetInput(loop, addvn, 1);
            }
            else {
                data.opSetInput(hiop, addvn, 1);
                data.opSetInput(loop, ptrvn, 1);
            }
        }

        private bool splitVarnode(SplitInstance inst)
        {
            // Test if -vn- can be readily split, if so, do the split
            Varnode vn = inst.vn;
            bool istemp;
            if (vn.isWritten()) {
                if (!vn.hasNoDescend()) return false; // Already linked in
                PcodeOp op = vn.getDef() ?? throw new BugException();
                switch (op.code()) {
                    case OpCode.CPUI_COPY:
                        if (!testDefiningCopy(inst, op, out istemp))
                            return false;
                        splitDefiningCopy(inst, op, istemp);
                        break;
                    case OpCode.CPUI_PIECE:
                        if (!testPiece(inst, op))
                            return false;
                        splitPiece(inst, op);
                        break;
                    case OpCode.CPUI_LOAD:
                        if (!testLoad(inst, op))
                            return false;
                        splitLoad(inst, op);
                        break;
                    case OpCode.CPUI_INT_ZEXT:
                        if (!testZext(inst, op))
                            return false;
                        splitZext(inst, op);
                        break;
                    default:
                        return false;
                }
                data.opDestroy(op);
            }
            else {
                if (!vn.isFree()) return false;    // Make sure vn is not already a marked input
                PcodeOp op = vn.loneDescend() ?? throw new BugException();
                if (op == (PcodeOp)null)  // vn must be read exactly once
                    return false;
                switch (op.code()) {
                    case OpCode.CPUI_COPY:
                        if (!testReadingCopy(inst, op, out istemp))
                            return false;
                        splitReadingCopy(inst, op, istemp);
                        break;
                    case OpCode.CPUI_SUBPIECE:
                        if (!testSubpiece(inst, op))
                            return false;
                        splitSubpiece(inst, op);
                        return true;        // Do not destroy op, it has been transformed
                    case OpCode.CPUI_STORE:
                        if (!testStore(inst, op))
                            return false;
                        splitStore(inst, op);
                        break;
                    default:
                        return false;
                }
                data.opDestroy(op);    // Original op is now dead
            }
            return true;
        }

        private void splitRecord(PreferSplitRecord rec)
        {
            Address addr = rec.storage.getAddr();
            VarnodeLocSet::const_iterator iter, enditer;

            SplitInstance inst = new SplitInstance((Varnode)null,rec.splitoffset);
            iter = data.beginLoc((int)rec.storage.size, addr);
            enditer = data.endLoc((int)rec.storage.size, addr);
            while (iter != enditer) {
                inst.vn = *iter;
                ++iter;
                inst.lo = (Varnode)null;
                inst.hi = (Varnode)null;
                if (splitVarnode(inst)) {
                    // If we found something, regenerate iterators, as they may be stale
                    iter = data.beginLoc((int)rec.storage.size, addr);
                    enditer = data.endLoc((int)rec.storage.size, addr);
                }
            }
        }

        private bool testTemporary(SplitInstance inst)
        {
            PcodeOp op = inst.vn.getDef() ?? throw new BugException();
            switch (op.code()) {
                case OpCode.CPUI_PIECE:
                    if (!testPiece(inst, op))
                        return false;
                    break;
                case OpCode.CPUI_LOAD:
                    if (!testLoad(inst, op))
                        return false;
                    break;
                case OpCode.CPUI_INT_ZEXT:
                    if (!testZext(inst, op))
                        return false;
                    break;
                default:
                    return false;
            }
            IEnumerator<PcodeOp> iter = inst.vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp readop = iter.Current;
                switch (readop.code()) {
                    case OpCode.CPUI_SUBPIECE:
                        if (!testSubpiece(inst, readop))
                            return false;
                        break;
                    case OpCode.CPUI_STORE:
                        if (!testStore(inst, readop))
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        private void splitTemporary(SplitInstance inst)
        {
            Varnode vn = inst.vn;
            PcodeOp op = vn.getDef() ?? throw new BugException();
            switch (op.code()) {
                case OpCode.CPUI_PIECE:
                    splitPiece(inst, op);
                    break;
                case OpCode.CPUI_LOAD:
                    splitLoad(inst, op);
                    break;
                case OpCode.CPUI_INT_ZEXT:
                    splitZext(inst, op);
                    break;
                default:
                    break;
            }

            while (true) {
                IEnumerator<PcodeOp> opEnumerator = vn.beginDescend();
                if (!opEnumerator.MoveNext()) break;
                PcodeOp readop = opEnumerator.Current;
                switch (readop.code()) {
                    case OpCode.CPUI_SUBPIECE:
                        splitSubpiece(inst, readop);
                        break;
                    case OpCode.CPUI_STORE:
                        splitStore(inst, readop);
                        data.opDestroy(readop);
                        break;
                    default:
                        break;
                }
            }
            data.opDestroy(op);
        }

        public void init(Funcdata fd,List<PreferSplitRecord> rec)
        {
            data = fd;
            records = rec;
        }

        public PreferSplitRecord? findRecord(Varnode vn)
        {
            // Find the split record that applies to -vn-, otherwise return null
            PreferSplitRecord templ = new PreferSplitRecord();
            templ.storage.space = vn.getSpace();
            templ.storage.size = (uint)vn.getSize();
            templ.storage.offset = vn.getOffset();
            IEnumerator<PreferSplitRecord> iter;
            iter = lower_bound(records.begin(), records.end(), templ);
            if (iter == records.end())
                return (PreferSplitRecord)null;
            if (templ < iter.Current)
                return (PreferSplitRecord)null;
            return iter.Current;
        }

        public static void initialize(List<PreferSplitRecord> records)
        {
            records.Sort();
        }

        public void split()
        {
            for (int i = 0; i < records.size(); ++i)
                splitRecord(records[i]);
        }

        public void splitAdditional()
        {
            List<PcodeOp> defops = new List<PcodeOp>();
            for (int i = 0; i < tempsplits.size(); ++i) {
                PcodeOp op = tempsplits[i]; // Look at everything connected to COPYs in -tempsplits-
                if (op.isDead()) continue;
                Varnode vn = op.getIn(0);
                if (vn.isWritten()) {
                    PcodeOp defop = vn.getDef() ?? throw new BugException();
                    if (defop.code() == OpCode.CPUI_SUBPIECE) {
                        // SUBPIECEs flowing into the COPY
                        Varnode invn = defop.getIn(0);
                        if (invn.getSpace().getType() == spacetype.IPTR_INTERNAL) // Might be from a temporary that needs further splitting
                            defops.Add(defop);
                    }
                }
                IEnumerator<PcodeOp> iter = op.getOut().beginDescend();
                while (iter.MoveNext()) {
                    PcodeOp defop = iter.Current;
                    if (defop.code() == OpCode.CPUI_PIECE) {
                        // COPY flowing into PIECEs
                        Varnode outvn = defop.getOut();
                        if (outvn.getSpace().getType() == spacetype.IPTR_INTERNAL) // Might be to a temporary that needs further splitting
                            defops.Add(defop);
                    }
                }
            }
            for (int i = 0; i < defops.size(); ++i) {
                PcodeOp op = defops[i];
                if (op.isDead()) continue;
                if (op.code() == OpCode.CPUI_PIECE) {
                    int splitoff;
                    Varnode vn = op.getOut();
                    if (vn.getSpace().isBigEndian())
                        splitoff = op.getIn(0).getSize();
                    else
                        splitoff = op.getIn(1).getSize();
                    SplitInstance inst = new SplitInstance(vn, splitoff);
                    if (testTemporary(inst))
                        splitTemporary(inst);
                }
                else if (op.code() == OpCode.CPUI_SUBPIECE) {
                    int splitoff;
                    Varnode vn = op.getIn(0);
                    ulong suboff = op.getIn(1).getOffset();
                    if (vn.getSpace().isBigEndian()) {
                        if (suboff == 0)
                            splitoff = vn.getSize() - op.getOut().getSize();
                        else
                            splitoff = vn.getSize() - (int)suboff;
                    }
                    else {
                        if (suboff == 0)
                            splitoff = op.getOut().getSize();
                        else
                            splitoff = (int)suboff;
                    }
                    SplitInstance inst = new SplitInstance(vn, splitoff);
                    if (testTemporary(inst))
                        splitTemporary(inst);
                }
            }
        }
    }
}
