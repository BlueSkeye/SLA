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
    internal class Next2Symbol : SpecificSymbol
    {
        private AddrSpace const_space;
        private PatternExpression patexp;
        
        public Next2Symbol()
        {
            patexp = (PatternExpression)null;
        }

        public Next2Symbol(string nm,AddrSpace cspc)
        {
            const_space = cspc;
            patexp = new Next2InstructionValue();
            patexp.layClaim();
        }

        ~Next2Symbol()
        {
            if (patexp != (PatternExpression)null)
                PatternExpression.release(patexp);
        }

        public override VarnodeTpl getVarnode()
        { // Return instruction offset after next instruction offset as a constant
            ConstTpl spc = new ConstTpl(const_space);
            ConstTpl off = new ConstTpl(ConstTpl.const_type.j_next2);
            ConstTpl sz_zero = new ConstTpl();
            return new VarnodeTpl(spc, off, sz_zero);
        }

        public override PatternExpression getPatternExpression() => patexp;

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = walker.getCurSpace();
            hand.offset_space = (AddrSpace)null;
            hand.offset_offset = walker.getN2addr().getOffset(); // Get instruction address after next instruction
            hand.size = hand.space.getAddrSize();
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            long val = (long)walker.getN2addr().getOffset();
            s.Write($"0x{val:X}");
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.next2_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<next2_sym");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<next2_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans.getConstantSpace();
            patexp = new Next2InstructionValue();
            patexp.layClaim();
        }
    }
}
