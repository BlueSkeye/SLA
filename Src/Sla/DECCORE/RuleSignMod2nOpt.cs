﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSignMod2nOpt : Rule
    {
        public RuleSignMod2nOpt(string g)
            : base(g, 0, "signmod2nopt")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RuleSignMod2nOpt(getGroup());
        }

        /// \class RuleSignMod2nOpt
        /// \brief Convert INT_SREM forms:  `(V + (sign >> (64-n)) & (2^n-1)) - (sign >> (64-n)  =>  V s% 2^n`
        /// Note: `sign = V s>> 63`  The INT_AND may be performed on a truncated result and then reextended.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant()) return 0;
            int shiftAmt = (int)op.getIn(1).getOffset();
            Varnode? a = checkSignExtraction(op.getIn(0));
            if (a == (Varnode)null || a.isFree()) return 0;
            Varnode correctVn = op.getOut();
            int n = a.getSize() * 8 - shiftAmt;
            ulong mask = 1;
            mask = (mask << n) - 1;
            IEnumerator<PcodeOp> iter = correctVn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp multop = iter.Current;
                if (multop.code() != OpCode.CPUI_INT_MULT) continue;
                Varnode negone = multop.getIn(1);
                if (!negone.isConstant()) continue;
                if (negone.getOffset() != Globals.calc_mask((uint)correctVn.getSize())) continue;
                PcodeOp? baseOp = multop.getOut().loneDescend();
                if (baseOp == (PcodeOp)null) continue;
                if (baseOp.code() != OpCode.CPUI_INT_ADD) continue;
                int slot = 1 - baseOp.getSlot(multop.getOut());
                Varnode andOut = baseOp.getIn(slot);
                if (!andOut.isWritten()) continue;
                PcodeOp andOp = andOut.getDef() ?? throw new ApplicationException();
                int truncSize = -1;
                if (andOp.code() == OpCode.CPUI_INT_ZEXT) {
                    // Look for intervening extension after INT_AND
                    andOut = andOp.getIn(0);
                    if (!andOut.isWritten()) continue;
                    andOp = andOut.getDef() ?? throw new ApplicationException();
                    if (andOp.code() != OpCode.CPUI_INT_AND) continue;
                    truncSize = andOut.getSize();      // If so we have a truncated form
                }
                else if (andOp.code() != OpCode.CPUI_INT_AND)
                    continue;

                Varnode constVn = andOp.getIn(1) ?? throw new ApplicationException();
                if (!constVn.isConstant()) continue;
                if (constVn.getOffset() != mask) continue;
                Varnode addOut = andOp.getIn(0) ?? throw new ApplicationException();
                if (!addOut.isWritten()) continue;
                PcodeOp addOp = addOut.getDef() ?? throw new ApplicationException();
                if (addOp.code() != OpCode.CPUI_INT_ADD) continue;
                // Search for "a" as one of the inputs to addOp
                int aSlot;
                for (aSlot = 0; aSlot < 2; ++aSlot) {
                    Varnode vn = addOp.getIn(aSlot);
                    if (truncSize >= 0) {
                        if (!vn.isWritten()) continue;
                        PcodeOp subOp = vn.getDef() ?? throw new ApplicationException();
                        if (subOp.code() != OpCode.CPUI_SUBPIECE) continue;
                        if (subOp.getIn(1).getOffset() != 0) continue;
                        vn = subOp.getIn(0);
                    }
                    if (a == vn) break;
                }
                if (aSlot > 1) continue;
                // Verify that the other input to addOp is an INT_RIGHT by shiftAmt
                Varnode? extVn = addOp.getIn(1 - aSlot);
                if (!extVn.isWritten()) continue;
                PcodeOp shiftOp = extVn.getDef() ?? throw new ApplicationException();
                if (shiftOp.code() != OpCode.CPUI_INT_RIGHT) continue;
                constVn = shiftOp.getIn(1) ?? throw new ApplicationException(); ;
                if (!constVn.isConstant()) continue;
                int shiftval = (int)constVn.getOffset();
                if (truncSize >= 0)
                    shiftval += (a.getSize() - truncSize) * 8;
                if (shiftval != shiftAmt) continue;
                // Verify that the input to INT_RIGHT is a sign extraction of "a"
                extVn = checkSignExtraction(shiftOp.getIn(0) ?? throw new ApplicationException())
                     ?? throw new ApplicationException();
                if (extVn == (Varnode)null) continue;
                if (truncSize >= 0) {
                    if (!extVn.isWritten()) continue;
                    PcodeOp subOp = extVn.getDef() ?? throw new ApplicationException();
                    if (subOp.code() != OpCode.CPUI_SUBPIECE) continue;
                    if ((int)subOp.getIn(1).getOffset() != truncSize) continue;
                    extVn = subOp.getIn(0);
                }
                if (a != extVn) continue;

                data.opSetOpcode(baseOp, OpCode.CPUI_INT_SREM);
                data.opSetInput(baseOp, a, 0);
                data.opSetInput(baseOp, data.newConstant(a.getSize(), mask + 1), 1);
                return 1;
            }
            return 0;
        }

        /// \brief Verify that the given Varnode is a sign extraction of the form `V s>> 63`
        ///
        /// If not, null is returned.  Otherwise the Varnode whose sign is extracted is returned.
        /// \param outVn is the given Varnode
        /// \return the Varnode being extracted or null
        public static Varnode? checkSignExtraction(Varnode outVn)
        {
            if (!outVn.isWritten()) return 0;
            PcodeOp signOp = outVn.getDef() ?? throw new ApplicationException();
            if (signOp.code() != OpCode.CPUI_INT_SRIGHT)
                return (Varnode)null;
            Varnode constVn = signOp.getIn(1) ?? throw new ApplicationException();
            if (!constVn.isConstant())
                return (Varnode)null;
            int val = (int)constVn.getOffset();
            Varnode resVn = signOp.getIn(0) ?? throw new ApplicationException();
            int insize = resVn.getSize();
            return (val != insize * 8 - 1) ? (Varnode)null : resVn;
        }
    }
}
