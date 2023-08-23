using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintVarnodeCopy : UnifyConstraint
    {
        private int oldvarindex;
        private int newvarindex;
        
        public ConstraintVarnodeCopy(int oldind, int newind)
        {
            oldvarindex = oldind;
            newvarindex = newind;
            maxnum = (oldvarindex > newvarindex) ? oldvarindex : newvarindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintVarnodeCopy(oldvarindex, newvarindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Varnode vn = state.data(oldvarindex).getVarnode();
            state.data(newvarindex).setVarnode(vn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[oldvarindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
            typelist[newvarindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => oldvarindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.WriteLine($"{printstate.getName(newvarindex)} = {printstate.getName(oldvarindex)};");
        }
    }
}
