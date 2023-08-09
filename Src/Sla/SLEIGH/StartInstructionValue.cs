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

        public override long getValue(ParserWalker walker)
        {
            return (long)AddrSpace.byteToAddress(walker.getAddr().getOffset(), walker.getAddr().getSpace().getWordSize());
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(long val) => new TokenPattern();

        public override long minValue() => (long)0;

        public override long maxValue() => (long)0;

        public override void saveXml(ostream s) 
        {
            s << "<start_exp/>";
        }

        public override void restoreXml(Element el, Translate trans)
        {
        }
    }
}
