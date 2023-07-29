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

        public override symbol_type getType() => userop_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<userop";
            SleighSymbol::saveXmlHeader(s);
            s << " index=\"" << dec << index << "\"";
            s << "/>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<userop_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            istringstream s(el.getAttributeValue("index"));
            s.unsetf(ios::dec | ios::hex | ios::oct);
            s >> index;
        }
    }
}
