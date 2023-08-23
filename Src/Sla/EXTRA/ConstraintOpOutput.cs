using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintOpOutput : UnifyConstraint
    {
        // Move from op to its output varnode
        private int opindex;           // Which op
        private int varnodeindex;      // Label of output varnode
        
        public ConstraintOpOutput(int oind, int vind)
        {
            opindex = oind;
            varnodeindex = vind;
            maxnum = (opindex > varnodeindex) ? opindex : varnodeindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpOutput(opindex, varnodeindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = op.getOut();
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
            s.WriteLine($"{printstate.getName(varnodeindex)} = {printstate.getName(opindex)}.getOut();");
        }
    }
}
