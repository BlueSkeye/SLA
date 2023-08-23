using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class EpsilonSymbol : PatternlessSymbol
    {
        // Another name for zero pattern/value
        private AddrSpace const_space;
        
        public EpsilonSymbol()
        {
        }

        public EpsilonSymbol(string nm,AddrSpace spc)
            : base(nm)
        {
            const_space = spc;
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = const_space;
            hand.offset_space = (AddrSpace)null; // Not a dynamic value
            hand.offset_offset = 0;
            hand.size = 0;      // Cannot provide size
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            s.Write('0');
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.epsilon_symbol;

        public override VarnodeTpl getVarnode()
        {
            VarnodeTpl res = new VarnodeTpl(new ConstTpl(const_space),
                new ConstTpl(ConstTpl.const_type.real, 0), new ConstTpl(ConstTpl.const_type.real, 0));
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s.Write("<epsilon_sym");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<epsilon_sym_head");
            base.saveXmlHeader(s);
            s.Write("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans.getConstantSpace();
        }
    }
}
