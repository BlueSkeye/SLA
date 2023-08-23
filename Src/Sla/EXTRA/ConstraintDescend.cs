using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintDescend : UnifyConstraint
    {
        private int opindex;
        private int varindex;
        
        public ConstraintDescend(int oind, int vind)
        {
            opindex = oind;
            varindex = vind;
            maxnum = (opindex > varindex) ? opindex : varindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintDescend(opindex, varindex)).copyid(this);

        public override void buildTraverseState(UnifyState state)
        {
            if (uniqid != state.numTraverse())
                throw new LowlevelError("Traverse id does not match index");
            TraverseConstraint newt = new TraverseDescendState(uniqid);
            state.registerTraverseConstraint(newt);
        }

        public override void initialize(UnifyState state)
        {
            TraverseDescendState traverse = (TraverseDescendState)state.getTraverse(uniqid);
            Varnode vn = state.data(varindex).getVarnode();
            traverse.initialize(vn);
        }

        public override bool step(UnifyState state)
        {
            TraverseDescendState traverse = (TraverseDescendState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = traverse.getCurrentOp();
            state.data(opindex).setOp(op);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[varindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => opindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.WriteLine($"list<PcodeOp *>::const_iterator iter{printstate.getDepth()},enditer{printstate.getDepth()};");
            printstate.printIndent(s);
            s.WriteLine($"iter{printstate.getDepth()} = {printstate.getName(varindex)}.beginDescend();");
            printstate.printIndent(s);
            s.WriteLine($"enditer{printstate.getDepth()} = {printstate.getName(varindex)}.endDescend();");
            printstate.printIndent(s);
            s.WriteLine($"while(iter{printstate.getDepth()} != enditer{printstate.getDepth()}) {{");
            printstate.incDepth();  // permanent increase in depth
            printstate.printIndent(s);
            s.WriteLine($"{printstate.getName(opindex)} = *iter{(printstate.getDepth() - 1)}");
            printstate.printIndent(s);
            s.WriteLine($"++iter{(printstate.getDepth() - 1)}");
        }
    }
}
