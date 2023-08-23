
namespace Sla.SLEIGH
{
    internal class RightShiftExpression : BinaryExpression
    {
        public RightShiftExpression()
        {
        }
        
        public RightShiftExpression(PatternExpression l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override long getValue(ParserWalker walker)
        {
            long leftval = getLeft().getValue(walker);
            long rightval = getRight().getValue(walker);
            return leftval >> (int)rightval;
        }

        public override long getSubValue(List<long> replace,int listpos)
        {
            long leftval = getLeft().getSubValue(replace, listpos); // Must be left first
            long rightval = getRight().getSubValue(replace, listpos);
            return leftval >> (int)rightval;
        }

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<rshift_exp>");
            base.saveXml(s);
            s.WriteLine("</rshift_exp>");
        }
    }
}
