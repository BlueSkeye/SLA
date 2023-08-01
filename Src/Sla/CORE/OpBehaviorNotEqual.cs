using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorNotEqual : OpBehavior
    {
        public OpBehaviorNotEqual()
            : base(OpCode.CPUI_INT_NOTEQUAL, false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            ulong res = in1 != in2 ? 1UL : 0;
            return res;
        }
    }
}
