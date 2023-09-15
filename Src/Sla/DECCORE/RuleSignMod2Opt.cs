using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSignMod2Opt : Rule
    {
        public RuleSignMod2Opt(string g)
            : base(g, 0, "signmod2opt")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RuleSignMod2Opt(getGroup());
        }

        /// \class RuleSignMod2Opt
        /// \brief Convert INT_SREM form:  `(V - sign)&1 + sign  =>  V s% 2`
        ///
        /// Note: `sign = V s>> 63`  The INT_AND may be performed on a truncated result and then reextended.
        /// This is a specialized form of RuleSignMod2nOpt.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_AND);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode constVn = op.getIn(1) ?? throw new ApplicationException();
            if (!constVn.isConstant()) return 0;
            if (constVn.getOffset() != 1) return 0;
            Varnode addOut = op.getIn(0) ?? throw new ApplicationException();
            if (!addOut.isWritten()) return 0;
            PcodeOp addOp = addOut.getDef() ?? throw new ApplicationException();
            if (addOp.code() != OpCode.CPUI_INT_ADD) return 0;
            int multSlot;
            PcodeOp? multOp = null;
            bool trunc = false;
            for (multSlot = 0; multSlot < 2; ++multSlot) {
                Varnode vn = addOp.getIn(multSlot) ?? throw new ApplicationException();
                if (!vn.isWritten()) 
                    continue;
                multOp = vn.getDef() ?? throw new ApplicationException();
                if (multOp.code() != OpCode.CPUI_INT_MULT)
                    continue;
                constVn = multOp.getIn(1) ?? throw new ApplicationException();
                if (!constVn.isConstant())
                    continue;
                // Check for INT_MULT by -1
                if (constVn.getOffset() == Globals.calc_mask((uint)constVn.getSize()))
                    break;
            }
            if (multSlot > 1) return 0;
            Varnode? @base = RuleSignMod2nOpt.checkSignExtraction(
                (multOp ?? throw new ApplicationException()).getIn(0));
            if (@base == (Varnode)null) return 0;
            Varnode otherBase = addOp.getIn(1 - multSlot) ?? throw new ApplicationException();
            if (@base != otherBase) {
                if (!@base.isWritten() || !otherBase.isWritten()) return 0;
                PcodeOp subOp = @base.getDef() ?? throw new ApplicationException();
                if (subOp.code() != OpCode.CPUI_SUBPIECE) return 0;
                int truncAmt = (int)subOp.getIn(1).getOffset();
                if (truncAmt + @base.getSize() != subOp.getIn(0).getSize()) return 0; // Must truncate all but high part
                @base = subOp.getIn(0);
                subOp = otherBase.getDef() ?? throw new ApplicationException();
                if (subOp.code() != OpCode.CPUI_SUBPIECE) return 0;
                if (subOp.getIn(1).getOffset() != 0) return 0;
                otherBase = subOp.getIn(0);
                if (otherBase != @base) return 0;
                trunc = true;
            }
            if (@base.isFree()) return 0;
            Varnode andOut = op.getOut() ?? throw new ApplicationException();
            if (trunc) {
                PcodeOp? extOp = andOut.loneDescend();
                if (extOp == (PcodeOp)null || extOp.code() != OpCode.CPUI_INT_ZEXT) return 0;
                andOut = extOp.getOut() ?? throw new ApplicationException();
            }
            IEnumerator<PcodeOp> iter = andOut.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp rootOp = iter.Current;
                if (rootOp.code() != OpCode.CPUI_INT_ADD) continue;
                int slot = rootOp.getSlot(andOut);
                otherBase = RuleSignMod2nOpt.checkSignExtraction(rootOp.getIn(1 - slot));
                if (otherBase != @base) continue;
                data.opSetOpcode(rootOp, OpCode.CPUI_INT_SREM);
                data.opSetInput(rootOp, @base, 0);
                data.opSetInput(rootOp, data.newConstant(@base.getSize(), 2), 1);
                return 1;
            }
            return 0;
        }
    }
}
