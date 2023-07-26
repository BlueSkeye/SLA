using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class SymbolCompare
    {
        internal bool operator/*()*/(SleighSymbol a, SleighSymbol b)
        {
            return (a->getName() < b->getName());
        }
    }
}
