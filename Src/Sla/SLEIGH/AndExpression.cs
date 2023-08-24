
namespace Sla.SLEIGH
{
    internal class AndExpression : BinaryExpression
    {
        public AndExpression()
        {
        }
        
        public AndExpression(PatternExpression l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override long getValue(ParserWalker walker)
        {
            long leftval = getLeft().getValue(walker);
            long rightval = getRight().getValue(walker);
            return leftval & rightval;
        }

        public override long getSubValue(List<long> replace, int listpos)
        {
            long leftval = getLeft().getSubValue(replace, listpos); // Must be left first
            long rightval = getRight().getSubValue(replace, listpos);
            return leftval & rightval;
        }

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<and_exp>");
            base.saveXml(s);
            s.WriteLine("</and_exp>");
        }
    }
}
