using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class BitrangeSymbol : SleighSymbol
    {
        // A smaller bitrange within a varnode
        private VarnodeSymbol varsym;  // Varnode containing the bitrange
        private uint4 bitoffset;        // least significant bit of range
        private uint4 numbits;      // number of bits in the range
        
        public BitrangeSymbol()
        {
        }

        public BitrangeSymbol(string nm,VarnodeSymbol sym, uint4 bitoff,uint4 num)
            : base(nm)
        {
            varsym = sym;
            bitoffset = bitoff;
            numbits = num;
        }

        public VarnodeSymbol getParentSymbol() => varsym;

        public uint4 getBitOffset() => bitoffset;

        public uint4 numBits() => numbits;

        public override symbol_type getType() => bitrange_symbol;
    }
}
