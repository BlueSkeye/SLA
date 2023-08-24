using Sla.CORE;

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
            s.Write("<commit");
            Xml.a_v_u(s, "id", sym.getId());
            Xml.a_v_i(s, "num", num);
            Xml.a_v_u(s, "mask", mask);
            Xml.a_v_b(s, "flow", flow);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            uint id = uint.Parse(el.getAttributeValue("id"));
            num = int.Parse(el.getAttributeValue("num"));
            mask = uint.Parse(el.getAttributeValue("mask"));
            // Default is to flow.  flow=true
            flow = (el.getNumAttributes() != 4)
                || Xml.xml_readbool(el.getAttributeValue("flow"));
        }

        public override void apply(ParserWalkerChange walker)
        {
            walker.getParserContext().addCommit(sym, num, mask, flow, walker.getPoint());
        }

        public override ContextChange clone()
        {
            ContextCommit res = new ContextCommit();
            res.sym = sym;
            res.flow = flow;
            res.mask = mask;
            res.num = num;
            return res;
        }
    }
}
