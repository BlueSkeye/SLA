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
        private intb val;
        
        public ConstantValue()
        {
        }

        public ConstantValue(intb v)
        {
            val = v;
        }
        
        public override intb getValue(ParserWalker walker) => val;

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => TokenPattern();

        public override TokenPattern genPattern(intb v) => TokenPattern(val==v);

        public override intb minValue() => val;

        public override intb maxValue() => val;

        public override void saveXml(TextWriter s)
        {
            s << "<intb val=\"" << dec << val << "\"/>\n";
        }

        public override void restoreXml(Element el, Translate trans)
        {
            istringstream s(el.getAttributeValue("val"));
            s.unsetf(ios::dec | ios::hex | ios::oct);
            s >> val;
        }
    }
}
