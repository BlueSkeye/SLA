
namespace Sla.SLEIGH
{
    internal class NotExpression : UnaryExpression
    {
        public NotExpression()
        {
        }
        
        public NotExpression(PatternExpression u)
            : base(u)
        {
        }
        
        public override long getValue(ParserWalker walker)
        {
            long val = getUnary().getValue(walker);
            return ~val;
        }

        public override long getSubValue(List<long> replace,int listpos)
        {
            long val = getUnary().getSubValue(replace, listpos);
            return ~val;
        }

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<not_exp>");
            base.saveXml(s);
            s.WriteLine("</not_exp>");
        }
    }
}
