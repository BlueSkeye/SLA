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
    internal class ContextSymbol : ValueSymbol
    {
        private VarnodeSymbol vn;
        private uint4 low, high;        // into a varnode
        private bool flow;
        
        public ContextSymbol()
        {
        }

        public ContextSymbol(string nm,ContextField pate, VarnodeSymbol v,uint4 l, uint4 h,
            bool flow);

        public VarnodeSymbol getVarnode() => vn;

        public uint4 getLow() => low;

        public uint4 getHigh() => high;

        public bool getFlow() => flow;

        public override symbol_type getType() => context_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<context_sym";
            SleighSymbol::saveXmlHeader(s);
            s << " varnode=\"0x" << hex << vn->getId() << "\"";
            s << " low=\"" << dec << low << "\"";
            s << " high=\"" << high << "\"";
            a_v_b(s, "flow", flow);
            s << ">\n";
            patval->saveXml(s);
            s << "</context_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<context_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            ValueSymbol::restoreXml(el, trans);
            {
                uintm id;
                istringstream s(el->getAttributeValue("varnode"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> id;
                vn = (VarnodeSymbol*)trans->findSymbol(id);
            }
            {
                istringstream s(el->getAttributeValue("low"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> low;
            }
            {
                istringstream s(el->getAttributeValue("high"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> high;
            }
            flow = true;
            for (int4 i = el->getNumAttributes() - 1; i >= 0; --i)
            {
                if (el->getAttributeName(i) == "flow")
                {
                    flow = xml_readbool(el->getAttributeValue(i));
                    break;
                }
            }
        }
    }
}
