using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSlessToLess : Rule
    {
        public RuleSlessToLess(string g)
            : base(g, 0, "slesstoless")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSlessToLess(getGroup());
        }

        /// \class RuleSlessToLess
        /// \brief Convert INT_SLESS to INT_LESS when comparing positive values
        ///
        /// This also works converting INT_SLESSEQUAL to INT_LESSEQUAL.
        /// We use the non-zero mask to verify the sign bit is zero.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SLESS);
            oplist.Add(OpCode.CPUI_INT_SLESSEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn = op.getIn(0);
            int sz = vn.getSize();
            if (Globals.signbit_negative(vn.getNZMask(), sz)) return 0;
            if (Globals.signbit_negative(op.getIn(1).getNZMask(), sz)) return 0;

            if (op.code() == OpCode.CPUI_INT_SLESS)
                data.opSetOpcode(op, OpCode.CPUI_INT_LESS);
            else
                data.opSetOpcode(op, OpCode.CPUI_INT_LESSEQUAL);
            return 1;
        }
    }
}
