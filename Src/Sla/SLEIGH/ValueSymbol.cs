using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class ValueSymbol : FamilySymbol
    {
        protected PatternValue patval;
        
        public ValueSymbol()
        {
            patval = (PatternValue)null;
        }

        public ValueSymbol(string nm,PatternValue pv)
            : base(nm)
        {
            (patval = pv).layClaim();
        }

        ~ValueSymbol()
        {
            if (patval != (PatternValue)null)
                PatternExpression.release(patval);
        }

        public override PatternValue getPatternValue() => patval;

        public override PatternExpression getPatternExpression() => patval;

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = walker.getConstSpace();
            hand.offset_space = (AddrSpace)null;
            hand.offset_offset = (ulong)patval.getValue(walker);
            hand.size = 0;      // Cannot provide size
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            long val = patval.getValue(walker);
            if (val >= 0)
                s.Write($"0x{val:X}");
            else
                s.Write($"-0x{-val:X}");
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.value_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<value_sym");
            base.saveXmlHeader(s);
            s.WriteLine(">");
            patval.saveXml(s);
            s.WriteLine("</value_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<value_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new ApplicationException();
            patval = (PatternValue)PatternExpression.restoreExpression(iter.Current, trans);
            patval.layClaim();
        }
    }
}
