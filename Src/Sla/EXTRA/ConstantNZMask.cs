using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstantNZMask : RHSConstant
    {
        // A varnode's non-zero mask
        private  int varindex;
        
        public ConstantNZMask(int ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantNZMask(varindex);

        public override ulong getConstant(UnifyState state)
        {
            Varnode vn = state.data(varindex).getVarnode();
            return vn.getNZMask();
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s.Write($"{printstate.getName(varindex)}.getNZMask()");
        }
    }
}
