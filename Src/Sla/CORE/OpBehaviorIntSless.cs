using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntSless : OpBehavior
    {
        public OpBehaviorIntSless()
            : base(OpCode.CPUI_INT_SLESS, false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            ulong res, mask, bit1, bit2;

            if (sizein <= 0)
                res = 0;
            else
            {
                mask = 0x80;
                mask <<= 8 * (sizein - 1);
                bit1 = in1 & mask;      // Get the sign bits
                bit2 = in2 & mask;
                if (bit1 != bit2)
                    res = bit1 != 0 ? 1UL : 0;
                else
                    res = in1 < in2 ? 1UL : 0;
            }
            return res;
        }
    }
}
