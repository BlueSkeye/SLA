using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleBxor2NotEqual : Rule
    {
        public RuleBxor2NotEqual(string g)
            : base(g, 0, "bxor2notequal")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleBxor2NotEqual(getGroup());
        }

        /// \class RuleBxor2NotEqual
        /// \brief Eliminate BOOL_XOR:  `V ^^ W  =>  V != W`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_BOOL_XOR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            data.opSetOpcode(op, OpCode.CPUI_INT_NOTEQUAL);
            return 1;
        }
    }
}
