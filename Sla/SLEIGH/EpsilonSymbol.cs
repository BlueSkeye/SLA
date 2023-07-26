using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            hand.offset_space = (AddrSpace*)0; // Not a dynamic value
            hand.offset_offset = 0;
            hand.size = 0;      // Cannot provide size
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            s << '0';
        }

        public override symbol_type getType() => epsilon_symbol;

        public override VarnodeTpl getVarnode()
        {
            VarnodeTpl* res = new VarnodeTpl(ConstTpl(const_space),
                               ConstTpl(ConstTpl::real, 0),
                               ConstTpl(ConstTpl::real, 0));
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<epsilon_sym";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<epsilon_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            const_space = trans->getConstantSpace();
        }
    }
}
