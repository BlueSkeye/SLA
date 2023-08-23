using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class ConstantValue : PatternValue
    {
        private long val;
        
        public ConstantValue()
        {
        }

        public ConstantValue(long v)
        {
            val = v;
        }
        
        public override long getValue(ParserWalker walker) => val;

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(long v) => new TokenPattern(val==v);

        public override long minValue() => val;

        public override long maxValue() => val;

        public override void saveXml(TextWriter s)
        {
            s.WriteLine($"<long val=\"{val}\"/>");
        }

        public override void restoreXml(Element el, Translate trans)
        {
            val = long.Parse(el.getAttributeValue("val"));
        }
    }
}
