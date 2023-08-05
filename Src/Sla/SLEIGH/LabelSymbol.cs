using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class LabelSymbol : SleighSymbol
    {
        // A branch label
        private uint index;            // Local 1 up index of label
        private bool isplaced;      // Has the label been placed (not just referenced)
        private uint refcount;     // Number of references to this label
        
        public LabelSymbol(string nm,uint i)
            : base(nm)
        {
            index = i;
            refcount = 0;
            isplaced = false;
        }
        
        public uint getIndex() => index;

        public void incrementRefCount()
        {
            refcount += 1;
        }

        public uint getRefCount() => refcount;

        public void setPlaced()
        {
            isplaced = true;
        }

        public bool isPlaced() => isplaced;

        public override symbol_type getType() => SleighSymbol.symbol_type.label_symbol;
    }
}
