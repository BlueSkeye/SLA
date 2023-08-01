﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntLess : OpBehavior
    {
        public OpBehaviorIntLess()
            : base(OpCode.CPUI_INT_LESS,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => (in1 < in2) ? 1UL : 0;
    }
}
