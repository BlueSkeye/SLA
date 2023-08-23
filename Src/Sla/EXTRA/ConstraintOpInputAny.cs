using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintOpInputAny : UnifyConstraint
    {
        // Move from op to ANY of its input varnodes
        private int opindex;           // Which op
        private int varnodeindex;      // What to label input varnode
        
        public ConstraintOpInputAny(int oind, int vind)
        {
            opindex = oind;
            varnodeindex = vind;
            maxnum = (opindex > varnodeindex) ? opindex : varnodeindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpInputAny(opindex, varnodeindex)).copyid(this);

        public override void initialize(UnifyState state)
        {
            // Default initialization (with only 1 state)
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            PcodeOp op = state.data(opindex).getOp();
            traverse.initialize(op.numInput());   // Initialize total number of inputs
        }

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = op.getIn(traverse.getState());
            state.data(varnodeindex).setVarnode(vn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[varnodeindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => varnodeindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"for(int i{printstate.getDepth()}=0;i{printstate.getDepth()}<");
            s.WriteLine($"{printstate.getName(opindex)}.numInput();++i{printstate.getDepth()}) {{");
            printstate.incDepth();  // A permanent increase in depth
            printstate.printIndent(s);
            s.Write($"{printstate.getName(varnodeindex)} = {printstate.getName(opindex)}.getIn(i");
            s.WriteLine($"{printstate.getDepth() - 1)});");
        }
    }
}
