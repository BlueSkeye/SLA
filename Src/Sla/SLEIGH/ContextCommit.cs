using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ContextCommit : ContextChange
    {
        private TripleSymbol sym;
        private int num;           // Index of word containing context commit
        private uint mask;         // mask of bits in word being committed
        private bool flow;          // Whether the context "flows" from the point of change
        
        public ContextCommit()
        {
        }

        public ContextCommit(TripleSymbol s, int sbit, int ebit, bool fl)
        {
            sym = s;
            flow = fl;

            int shift;
            calc_maskword(sbit, ebit, out num, out shift, out mask);
        }

        public override void validate()
        {
        }

        public override void saveXml(TextWriter s)
        {
            s << "<commit";
            a_v_u(s, "id", sym.getId());
            a_v_i(s, "num", num);
            a_v_u(s, "mask", mask);
            a_v_b(s, "flow", flow);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            uint id;
            {
                istringstream s(el.getAttributeValue("id"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> id;
                sym = (TripleSymbol*)trans.findSymbol(id);
            }
            {
                istringstream s(el.getAttributeValue("num"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> num;
            }
            {
                istringstream s(el.getAttributeValue("mask"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> mask;
            }
            if (el.getNumAttributes() == 4)
                flow = xml_readbool(el.getAttributeValue("flow"));
            else
                flow = true;        // Default is to flow.  flow=true
        }

        public override void apply(ParserWalkerChange walker)
        {
            walker.getParserContext().addCommit(sym, num, mask, flow, walker.getPoint());
        }

        public override ContextChange clone()
        {
            ContextCommit* res = new ContextCommit();
            res.sym = sym;
            res.flow = flow;
            res.mask = mask;
            res.num = num;
            return res;
        }
    }
}
