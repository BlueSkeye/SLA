using Sla.CORE;

namespace Sla.DECCORE
{
    internal class Rule2Comp2Mult : Rule
    {
        public Rule2Comp2Mult(string g)
            : base(g,0,"2comp2mult")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new Rule2Comp2Mult(getGroup());
        }

        /// \class Rule2Comp2Mult
        /// \brief Eliminate INT_2COMP:  `-V  =>  V * -1`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_2COMP);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            data.opSetOpcode(op, OpCode.CPUI_INT_MULT);
            int size = op.getIn(0).getSize();
            Varnode negone = data.newConstant(size, Globals.calc_mask((uint)size));
            data.opInsertInput(op, negone, 1);
            return 1;
        }
    }
}
