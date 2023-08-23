using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintParamConstVal : UnifyConstraint
    {
        private int opindex;           // Which opcode
        private int slot;          // Which slot to examine for constant
        private ulong val;          // What value parameter must match
        
        public ConstraintParamConstVal(int oind, int sl, ulong v)
        {
            maxnum = opindex = oind;
            slot = sl;
            val = v;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintParamConstVal(opindex, slot, val)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = op.getIn(slot);
            if (!vn.isConstant()) return false;
            if (vn.getOffset() != (val & Globals.calc_mask(vn.getSize()))) return false;
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.WriteLine($"if (!{printstate.getName(opindex)}.getIn({slot}).isConstant())");
            printstate.printAbort(s);
            printstate.printIndent(s);
            s.Write($"if ({printstate.getName(opindex)}.getIn({slot}).getOffset() != 0x");
            s.Write($"{val:X} & Globals.calc_mask({printstate.getName(opindex)}.getIn(");
            s.WriteLine($"{slot}).getSize()))");
            printstate.printAbort(s);
        }
    }
}
