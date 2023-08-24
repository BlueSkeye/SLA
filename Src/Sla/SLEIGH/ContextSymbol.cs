using Sla.CORE;

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

        public ContextSymbol(string nm,ContextField pate, VarnodeSymbol v,uint l, uint h, bool flow)
            : base(nm, pate)
        {
            vn = v;
            low = l;
            high = h;
            this.flow = flow;
        }

        public VarnodeSymbol getVarnode() => vn;

        public uint getLow() => low;

        public uint getHigh() => high;

        public bool getFlow() => flow;

        public override symbol_type getType() => SleighSymbol.symbol_type.context_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<context_sym");
            base.saveXmlHeader(s);
            s.Write($" varnode=\"0x{vn.getId():X}\" low=\"{low}\" high=\"{high}\"");
            Xml.a_v_b(s, "flow", flow);
            s.WriteLine();
            patval.saveXml(s);
            s.WriteLine("</context_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<context_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            base.restoreXml(el, trans);
            uint id = uint.Parse(el.getAttributeValue("varnode"));
            vn = (VarnodeSymbol)trans.findSymbol(id);
            low = uint.Parse(el.getAttributeValue("low"));
            high = uint.Parse(el.getAttributeValue("high"));
            flow = true;
            for (int i = el.getNumAttributes() - 1; i >= 0; --i) {
                if (el.getAttributeName(i) == "flow") {
                    flow = Xml.xml_readbool(el.getAttributeValue(i));
                    break;
                }
            }
        }
    }
}
