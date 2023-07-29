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
    internal class StartInstructionValue : PatternValue
    {
        public StartInstructionValue()
        {
        }

        public override intb getValue(ParserWalker walker)
        {
            return (intb)AddrSpace::byteToAddress(walker.getAddr().getOffset(), walker.getAddr().getSpace()->getWordSize());
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(intb val) => new TokenPattern();

        public override intb minValue() => (intb)0;

        public override intb maxValue() => (intb)0;

        public override void saveXml(ostream s) 
        {
            s << "<start_exp/>";
        }

        public override void restoreXml(Element el, Translate trans)
        {
        }
    }
}
