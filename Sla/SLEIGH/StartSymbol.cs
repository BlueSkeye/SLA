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
    internal class StartSymbol : SpecificSymbol
    {
        private AddrSpace const_space;
        private PatternExpression patexp;
        
        public StartSymbol()
        {
            patexp = (PatternExpression*)0;
        }

        public StartSymbol(string nm,AddrSpace cspc)
        {
            const_space = cspc;
            patexp = new StartInstructionValue();
            patexp->layClaim();
        }

        ~StartSymbol()
        {
            if (patexp != (PatternExpression*)0)
                PatternExpression::release(patexp);
        }

        public override VarnodeTpl getVarnode()
        { // Returns current instruction offset as a constant
            ConstTpl spc(const_space);
            ConstTpl off(ConstTpl::j_start);
            ConstTpl sz_zero;
            return new VarnodeTpl(spc, off, sz_zero);
        }

        public override PatternExpression getPatternExpression() => patexp;

        public override void getFixedHandle(ref FixedHandle hand, ParserWalker walker)
        {
            hand.space = walker.getCurSpace();
            hand.offset_space = (AddrSpace*)0;
            hand.offset_offset = walker.getAddr().getOffset(); // Get starting address of instruction
            hand.size = hand.space->getAddrSize();
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            intb val = (intb)walker.getAddr().getOffset();
            s << "0x" << hex << val;
        }

        public override symbol_type getType() => start_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<start_sym";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<start_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans->getConstantSpace();
            patexp = new StartInstructionValue();
            patexp->layClaim();
        }
    }
}
