using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class FamilySymbol : TripleSymbol
    {
        public FamilySymbol()
        {
        }
        
        public FamilySymbol(string nm)
            : base(nm)
        {
        }
        
        public abstract PatternValue getPatternValue();
    }
}
