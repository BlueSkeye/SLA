using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintRemoveInput : UnifyConstraint
    {
        private int opindex;
        private RHSConstant slot;
        
        public ConstraintRemoveInput(int oind, RHSConstant sl)
        {
            opindex = oind;
            slot = sl;
            maxnum = opindex;
        }

        ~ConstraintRemoveInput()
        {
            // delete slot;
        }

        public override UnifyConstraint clone() 
            => (new ConstraintRemoveInput(opindex, slot.clone())).copyid(this);

        public override int getBaseIndex() => opindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata fd = state.getFunction();
            PcodeOp op = state.data(opindex).getOp();
            int slt = (int)slot.getConstant(state);
            fd.opRemoveInput(op, slt);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"data.opRemoveInput({printstate.getName(opindex)},");
            slot.writeExpression(s, printstate);
            s.WriteLine(");");
        }
    }
}
