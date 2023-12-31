﻿using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintParamConst : UnifyConstraint
    {
        private int opindex;           // Which opcode
        private int slot;          // Which slot to examine for constant
        private int constindex;        // Which varnode is the constant
        
        public ConstraintParamConst(int oind, int sl, int cind)
        {
            opindex = oind;
            slot = sl;
            constindex = cind;
            maxnum = (opindex > constindex) ? opindex : constindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintParamConst(opindex, slot, constindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = op.getIn(slot);
            if (!vn.isConstant()) return false;
            state.data(constindex).setConstant(vn.getOffset());
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[constindex] = new UnifyDatatype(UnifyDatatype.TypeKind.const_type);
        }

        public override int getBaseIndex() => constindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.WriteLine($"if (!{printstate.getName(opindex)}.getIn({slot}).isConstant())");
            printstate.printAbort(s);
            printstate.printIndent(s);
            s.Write($"{printstate.getName(constindex)} = ");
            s.WriteLine($"{printstate.getName(opindex)}.getIn({slot}).getOffset();");
        }
    }
}
