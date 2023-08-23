using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    // Action constraints, these must always step exactly once (returning true), and do their action
    internal class ConstraintNewOp : UnifyConstraint
    {
        private int newopindex;
        private int oldopindex;
        private bool insertafter;       // true if inserted AFTER oldop
        private OpCode opc;         // new opcode
        private int numparams;
        
        public ConstraintNewOp(int newind, int oldind, OpCode oc, bool iafter, int num)
        {
            newopindex = newind;
            oldopindex = oldind;
            opc = oc;
            insertafter = iafter;
            numparams = num;
            maxnum = (newind > oldind) ? newind : oldind;
        }

        public override UnifyConstraint clone()
            => (new ConstraintNewOp(newopindex, oldopindex, opc, insertafter, numparams)).copyid(this);

        public override int getBaseIndex() => newopindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata fd = state.getFunction();
            PcodeOp op = state.data(oldopindex).getOp();
            PcodeOp newop = fd.newOp(numparams, op.getAddr());
            fd.opSetOpcode(newop, opc);
            if (insertafter)
                fd.opInsertAfter(newop, op);
            else
                fd.opInsertBefore(newop, op);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[newopindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[oldopindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"{printstate.getName(newopindex)} = data.newOp({numparams}");
            s.WriteLine($",{printstate.getName(oldopindex)}.getAddr());");
            printstate.printIndent(s);
            s.WriteLine($"data.opSetOpcode({printstate.getName(newopindex)},CPUI_{Globals.get_opname(opc));");
            s.Write("data.opInsert");
            if (insertafter)
                s.Write("After(");
            else
                s.Write("Before(");
            s.WriteLine($"{printstate.getName(newopindex)},{printstate.getName(oldopindex)});");
        }
    }
}
