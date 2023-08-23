using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstantIsConstant : RHSConstant
    {
        private int varindex;
        
        public ConstantIsConstant(int ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantIsConstant(varindex);

        public override ulong getConstant(UnifyState state)
        {
            Varnode vn = state.data(varindex).getVarnode();
            return vn.isConstant() ? (ulong)1 : (ulong)0;
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s.Write($"(ulong){printstate.getName(varindex)}.isConstant()");
        }
    }
}
