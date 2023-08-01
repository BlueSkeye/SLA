using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntScarry : OpBehavior
    {
        public OpBehaviorIntScarry()
            : base(OpCode.CPUI_INT_SCARRY,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            ulong res = in1 + in2;

            uint a = (uint)(in1 >> (sizein * 8 - 1)) & 1; // Grab sign bit
            uint b = (uint)(in2 >> (sizein * 8 - 1)) & 1; // Grab sign bit
            uint r = (uint)(res >> (sizein * 8 - 1)) & 1; // Grab sign bit

            r ^= a;
            a ^= b;
            a ^= 1;
            r &= a;
            return (ulong)r;
        }
    }
}
