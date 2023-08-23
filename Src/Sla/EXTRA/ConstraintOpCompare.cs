using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintOpCompare : UnifyConstraint
    {
        private int op1index;
        private int op2index;
        private bool istrue;
        
        public ConstraintOpCompare(int op1ind, int op2ind, bool val)
        {
            op1index = op1ind;
            op2index = op2ind;
            istrue = val;
            maxnum = (op1index > op2index) ? op1index : op2index;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpCompare(op1index, op2index, istrue)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op1 = state.data(op1index).getOp();
            PcodeOp op2 = state.data(op2index).getOp();
            return ((op1 == op2) == istrue);
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[op1index] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[op2index] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
        }

        public override int getBaseIndex() => op1index;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"if ({printstate.getName(op1index)}");
            if (istrue)
                s.Write(" != ");
            else
                s.Write(" == ");
            s.WriteLine($"{printstate.getName(op2index)})");
            printstate.printAbort(s);
        }
    }
}
