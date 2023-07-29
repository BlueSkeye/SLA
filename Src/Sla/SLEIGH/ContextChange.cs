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

        protected static void calc_maskword(int4 sbit, int4 ebit, out int4 num, out int4 shift,
            out uintm mask)
        {
            num = sbit / (8 * sizeof(uintm));
            if (num != ebit / (8 * sizeof(uintm)))
                throw SleighError("Context field not contained within one machine int");
            sbit -= num * 8 * sizeof(uintm);
            ebit -= num * 8 * sizeof(uintm);

            shift = 8 * sizeof(uintm) - ebit - 1;
            mask = (~((uintm)0)) >> (sbit + shift);
            mask <<= shift;
        }
    }
}
