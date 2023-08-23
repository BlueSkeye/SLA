using Sla.CORE;
using Sla.SLEIGH;

namespace Sla.SLEIGH
{
    internal class UserOpSymbol : SleighSymbol
    {
        private uint index;
        
        public UserOpSymbol()
        {
        }

        public UserOpSymbol(string nm)
            : base(nm)
        {
            index = 0;
        }

        public void setIndex(uint ind)
        {
            index = ind;
        }

        public uint getIndex() => index;

        public override symbol_type getType() => SleighSymbol.symbol_type.userop_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<userop");
            base.saveXmlHeader(s);
            s.Write($" index=\"{index}\"");
            s.WriteLine("/>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<userop_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            index = uint.Parse(el.getAttributeValue("index"));
        }
    }
}
