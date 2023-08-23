using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintOpInput : UnifyConstraint
    {
        // Move from op to one of its input varnodes
        private int opindex;           // Which op
        private int varnodeindex;      // Which input varnode
        private int slot;          // Which slot to take
        
        public ConstraintOpInput(int oind, int vind, int sl)
        {
            opindex = oind;
            varnodeindex = vind;
            slot = sl;
            maxnum = (opindex > varnodeindex) ? opindex : varnodeindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpInput(opindex, varnodeindex, slot)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = op.getIn(slot);
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
            s.Write($"{printstate.getName(varnodeindex)} = {printstate.getName(opindex)}");
            s.WriteLine($".getIn({slot});");
        }
    }
}
