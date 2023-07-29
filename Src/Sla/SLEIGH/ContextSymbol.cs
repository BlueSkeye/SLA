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
        private uint low, high;        // into a varnode
        private bool flow;
        
        public ContextSymbol()
        {
        }

        public ContextSymbol(string nm,ContextField pate, VarnodeSymbol v,uint l, uint h,
            bool flow);

        public VarnodeSymbol getVarnode() => vn;

        public uint getLow() => low;

        public uint getHigh() => high;

        public bool getFlow() => flow;

        public override symbol_type getType() => context_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<context_sym";
            SleighSymbol::saveXmlHeader(s);
            s << " varnode=\"0x" << hex << vn.getId() << "\"";
            s << " low=\"" << dec << low << "\"";
            s << " high=\"" << high << "\"";
            a_v_b(s, "flow", flow);
            s << ">\n";
            patval.saveXml(s);
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
                uint id;
                istringstream s = new istringstream(el.getAttributeValue("varnode"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> id;
                vn = (VarnodeSymbol*)trans.findSymbol(id);
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("low"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> low;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("high"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> high;
            }
            flow = true;
            for (int i = el.getNumAttributes() - 1; i >= 0; --i)
            {
                if (el.getAttributeName(i) == "flow")
                {
                    flow = xml_readbool(el.getAttributeValue(i));
                    break;
                }
            }
        }
    }
}
