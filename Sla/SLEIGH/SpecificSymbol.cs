using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class SpecificSymbol : TripleSymbol
    {
        public SpecificSymbol()
        {
        }
        
        public SpecificSymbol(string nm)
            : base(nm)
        {
        }
        
        public abstract VarnodeTpl getVarnode();
    }
}
