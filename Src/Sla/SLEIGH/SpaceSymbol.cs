using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class SpaceSymbol : SleighSymbol
    {
        private AddrSpace space;
        
        public SpaceSymbol(AddrSpace spc)
            : base(spc.getName())
        {
            space = spc;
        }
        
        public AddrSpace getSpace() => space;

        public override symbol_type getType() => space_symbol;
    }
}
