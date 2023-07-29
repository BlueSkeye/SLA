using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class ContextChange
    {
        // Change to context command
        ~ContextChange()
        {
        }

        public abstract void validate();

        public abstract void saveXml(TextWriter s);

        public abstract void restoreXml(Element el, SleighBase trans);

        public abstract void apply(ParserWalkerChange walker);

        public abstract ContextChange clone();

        protected static void calc_maskword(int sbit, int ebit, out int num, out int shift,
            out uint mask)
        {
            num = sbit / (8 * sizeof(uint));
            if (num != ebit / (8 * sizeof(uint)))
                throw new SleighError("Context field not contained within one machine int");
            sbit -= num * 8 * sizeof(uint);
            ebit -= num * 8 * sizeof(uint);

            shift = 8 * sizeof(uint) - ebit - 1;
            mask = (uint.MaxValue) >> (sbit + shift);
            mask <<= shift;
        }
    }
}
