using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintNewUniqueOut : UnifyConstraint
    {
        private int opindex;
        private int newvarindex;
        private int sizevarindex;      // Negative is specific size, Positive is varnode index (for size)
        
        public ConstraintNewUniqueOut(int oind, int newvarind, int sizeind)
        {
            opindex = oind;
            newvarindex = newvarind;
            sizevarindex = sizeind;
            maxnum = (opindex > newvarindex) ? opindex : newvarindex;
            if (sizevarindex > maxnum)
                maxnum = sizevarindex;
        }

        public override UnifyConstraint clone()
            => (new ConstraintNewUniqueOut(opindex, newvarindex, sizevarindex)).copyid(this);

        public override int getBaseIndex() => newvarindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata fd = state.getFunction();
            PcodeOp op = state.data(opindex).getOp();
            int sz;
            if (sizevarindex < 0)
                sz = -sizevarindex;     // A specific size
            else {
                Varnode sizevn = state.data(sizevarindex).getVarnode();
                sz = sizevn.getSize();
            }
            Varnode newvn = fd.newUniqueOut(sz, op);
            state.data(newvarindex).setVarnode(newvn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[newvarindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
            if (sizevarindex >= 0)
                typelist[sizevarindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"{printstate.getName(newvarindex)} = data.newUniqueOut(");
            if (sizevarindex < 0)
                s.Write(-sizevarindex);
            else
                s.Write($"{printstate.getName(sizevarindex)}.getSize()");
            s.WriteLine($",{printstate.getName(opindex)});");
        }
    }
}
