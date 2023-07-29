using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class PatternlessSymbol : SpecificSymbol
    {
        private ConstantValue patexp;
        
        public PatternlessSymbol()
        {               // The void constructor must explicitly build
                        // the ConstantValue because it is not stored
                        // or restored via xml
            patexp = new ConstantValue((intb)0);
            patexp.layClaim();
        }

        public PatternlessSymbol(string nm)
            : base(nm)
        {
            patexp = new ConstantValue((intb)0);
            patexp.layClaim();
        }

        ~PatternlessSymbol()
        {
            PatternExpression::release(patexp);
        }

        public override PatternExpression getPatternExpression() => patexp;

        public override void saveXml(TextWriter s)
        {
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
        }
    }
}
