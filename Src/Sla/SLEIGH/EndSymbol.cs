using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class EndSymbol : SpecificSymbol
    {
        private AddrSpace const_space;
        private PatternExpression patexp;
        
        public EndSymbol()
        {
            patexp = (PatternExpression)null;
        }

        public EndSymbol(string nm,AddrSpace cspc)
        {
            const_space = cspc;
            patexp = new EndInstructionValue();
            patexp.layClaim();
        }

        ~EndSymbol()
        {
            if (patexp != (PatternExpression)null)
                PatternExpression.release(patexp);
        }

        public override VarnodeTpl getVarnode()
        { // Return next instruction offset as a constant
            ConstTpl spc = new ConstTpl(const_space);
            ConstTpl off = new ConstTpl(ConstTpl.const_type.j_next);
            ConstTpl sz_zero = new ConstTpl();
            return new VarnodeTpl(spc, off, sz_zero);
        }

        public override PatternExpression getPatternExpression() => patexp;

        public override void getFixedHandle(ref FixedHandle hand, ParserWalker walker)
        {
            hand.space = walker.getCurSpace();
            hand.offset_space = (AddrSpace)null;
            hand.offset_offset = walker.getNaddr().getOffset(); // Get starting address of next instruction
            hand.size = hand.space.getAddrSize();
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            long val = (long)walker.getNaddr().getOffset();
            s.Write($"0x{val:X}");
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.end_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<end_sym");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<end_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans.getConstantSpace();
            patexp = new EndInstructionValue();
            patexp.layClaim();
        }
    }
}
