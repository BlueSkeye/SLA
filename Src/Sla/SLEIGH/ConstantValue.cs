using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ConstantValue : PatternValue
    {
        private long val;
        
        public ConstantValue()
        {
        }

        public ConstantValue(long v)
        {
            val = v;
        }
        
        public override long getValue(ParserWalker walker) => val;

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => TokenPattern();

        public override TokenPattern genPattern(long v) => TokenPattern(val==v);

        public override long minValue() => val;

        public override long maxValue() => val;

        public override void saveXml(TextWriter s)
        {
            s << "<long val=\"" << dec << val << "\"/>\n";
        }

        public override void restoreXml(Element el, Translate trans)
        {
            istringstream s = new istringstream(el.getAttributeValue("val"));
            s.unsetf(ios::dec | ios::hex | ios::oct);
            s >> val;
        }
    }
}
