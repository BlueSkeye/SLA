using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class StartSymbol : SpecificSymbol
    {
        private AddrSpace const_space;
        private PatternExpression patexp;
        
        public StartSymbol()
        {
            patexp = (PatternExpression)null;
        }

        public StartSymbol(string nm,AddrSpace cspc)
        {
            const_space = cspc;
            patexp = new StartInstructionValue();
            patexp.layClaim();
        }

        ~StartSymbol()
        {
            if (patexp != (PatternExpression)null)
                PatternExpression.release(patexp);
        }

        public override VarnodeTpl getVarnode()
        {
            // Returns current instruction offset as a constant
            ConstTpl spc = new ConstTpl(const_space);
            ConstTpl off = new ConstTpl(ConstTpl.const_type.j_start);
            ConstTpl sz_zero;
            return new VarnodeTpl(spc, off, sz_zero);
        }

        public override PatternExpression getPatternExpression() => patexp;

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = walker.getCurSpace();
            hand.offset_space = (AddrSpace)null;
            hand.offset_offset = walker.getAddr().getOffset(); // Get starting address of instruction
            hand.size = hand.space.getAddrSize();
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            long val = (long)walker.getAddr().getOffset();
            s.Write($"0x{val:X}");
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.start_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<start_sym");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<start_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans.getConstantSpace();
            patexp = new StartInstructionValue();
            patexp.layClaim();
        }
    }
}
