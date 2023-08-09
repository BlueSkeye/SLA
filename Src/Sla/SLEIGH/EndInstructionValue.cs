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
    internal class EndInstructionValue : PatternValue
    {
        public EndInstructionValue()
        {
        }

        public override long getValue(ParserWalker walker)
        {
            return (long)AddrSpace.byteToAddress(walker.getNaddr().getOffset(), walker.getNaddr().getSpace().getWordSize());
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(long val) => new TokenPattern();

        public override long minValue() => (long)0;

        public override long maxValue() => (long)0;

        public override void saveXml(TextWriter s) 
        {
            s << "<end_exp/>";
        }

        public override void restoreXml(Element el, Translate trans)
        {
        }
    }
}
