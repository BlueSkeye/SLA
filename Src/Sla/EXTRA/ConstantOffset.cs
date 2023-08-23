using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstantOffset : RHSConstant
    {
        // A varnode's offset
        private int varindex;
        
        public ConstantOffset(int ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantOffset(varindex);

        public override ulong getConstant(UnifyState state)
        {
            Varnode vn = state.data(varindex).getVarnode();
            return vn.getOffset();
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s.Write($"{printstate.getName(varindex)}.getOffset()");
        }
    }
}
