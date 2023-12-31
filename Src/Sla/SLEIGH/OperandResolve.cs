﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal struct OperandResolve
    {
        internal List<OperandSymbol> operands;
        internal OperandResolve(List<OperandSymbol> ops)
        {
            operands = ops;
            @base = -1;
            offset = 0;
            cur_rightmost = -1;
            size = 0;
        }
        internal int @base;        // Current base operand (as we traverse the pattern equation from left to right)
        internal int offset;      // Bytes we have traversed from the LEFT edge of the current base
        internal int cur_rightmost; // (resulting) rightmost operand in our pattern
        internal int size;		// (resulting) bytes traversed from the LEFT edge of the rightmost
    }
}
