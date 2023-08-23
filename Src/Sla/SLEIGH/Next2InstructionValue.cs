using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class Next2InstructionValue : PatternValue
    {
        public Next2InstructionValue()
        {
        }

        public override long getValue(ParserWalker walker)
        {
            return (long)AddrSpace.byteToAddress(walker.getN2addr().getOffset(),
                walker.getN2addr().getSpace().getWordSize());
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(long val) => new TokenPattern();

        public override long minValue() => (long)0;
        
        public override long maxValue() => (long)0;

        public override void saveXml(TextWriter s) 
        {
            s.Write("<next2_exp/>");
        }

        public override void restoreXml(Element el, Translate trans)
        {
        }
    }
}
