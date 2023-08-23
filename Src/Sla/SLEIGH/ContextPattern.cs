using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class ContextPattern : DisjointPattern
    {
        // Matches the context bitstream
        private PatternBlock? maskvalue;
        
        protected override PatternBlock? getBlock(bool context)
            => context? maskvalue : (PatternBlock)null;

        public ContextPattern()
        {
            maskvalue = (PatternBlock)null;
        }

        public ContextPattern(PatternBlock mv)
        {
            maskvalue = mv;
        }

        public PatternBlock? getBlock() => maskvalue;

        ~ContextPattern()
        {
            /// if (maskvalue != (PatternBlock)null) delete maskvalue;
        }

        public override Pattern simplifyClone() => new ContextPattern(maskvalue.clone());

        public override void shiftInstruction(int sa)
        {
        }

        public override Pattern doOr(Pattern b, int sa)
        {
            ContextPattern? b2 = b as ContextPattern;
            if (b2 == (ContextPattern)null)
                return b.doOr(this, -sa);

            return new OrPattern((DisjointPattern)simplifyClone(), (DisjointPattern)b2.simplifyClone());
        }

        public override Pattern doAnd(Pattern b, int sa)
        {
            ContextPattern? b2 = b as ContextPattern;
            if (b2 == (ContextPattern)null)
                return b.doAnd(this, -sa);

            PatternBlock resblock = maskvalue.intersect(b2.maskvalue);
            return new ContextPattern(resblock);
        }

        public override Pattern commonSubPattern(Pattern b, int sa)
        {
            ContextPattern? b2 = b as ContextPattern;
            if (b2 == (ContextPattern)null)
                return b.commonSubPattern(this, -sa);

            PatternBlock resblock = maskvalue.commonSubPattern(b2.maskvalue);
            return new ContextPattern(resblock);
        }

        public override bool isMatch(ParserWalker walker) => maskvalue.isContextMatch(walker);

        public override bool alwaysTrue() => maskvalue.alwaysTrue();

        public override bool alwaysFalse() => maskvalue.alwaysFalse();

        public override bool alwaysInstructionTrue() => true;

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<context_pat>");
            maskvalue.saveXml(s);
            s.WriteLine("</context_pat>");
        }

        public override void restoreXml(Element el)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new ApplicationException();
            maskvalue = new PatternBlock(true);
            maskvalue.restoreXml(iter.Current);
        }
    }
}
