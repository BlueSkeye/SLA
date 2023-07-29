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
    internal class FlowRefSymbol : SpecificSymbol
    {
        private AddrSpace const_space;
        
        public FlowRefSymbol()
        {
        }

        public FlowRefSymbol(string nm,AddrSpace cspc)
            : base(nm)

        {
            const_space = cspc;
        }

        public override VarnodeTpl getVarnode()
        {
            ConstTpl spc(const_space);
            ConstTpl off(ConstTpl::j_flowref);
            ConstTpl sz_zero;
            return new VarnodeTpl(spc, off, sz_zero);
        }

        public override PatternExpression getPatternExpression()
        {
            throw new SleighError("Cannot use symbol in pattern");
        }

        public override void getFixedHandle(ref FixedHandle hand, ParserWalker walker)
        {
            Address refAddr = walker.getRefAddr();
            hand.space = const_space;
            hand.offset_space = (AddrSpace)null;
            hand.offset_offset = refAddr.getOffset();
            hand.size = refAddr.getAddrSize();
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            long val = (long)walker.getRefAddr().getOffset();
            s << "0x" << hex << val;
        }

        public override symbol_type getType() => start_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<flowref_sym";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<flowref_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans.getConstantSpace();
        }
    }
}
