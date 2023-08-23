
namespace Sla.EXTRA
{
    internal class ConstantNamed : RHSConstant
    {
        private int constindex;
        
        public ConstantNamed(int id)
        {
            constindex = id;
        }
        
        public int getId() => constindex;

        public override RHSConstant clone() => new ConstantNamed(constindex);

        public override ulong getConstant(UnifyState state) => state.data(constindex).getConstant();

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s.Write(printstate.getName(constindex));
        }
    }
}
