using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntCarry : OpBehavior
    {
        public OpBehaviorIntCarry()
            : base(OpCode.CPUI_INT_CARRY,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => (in1 > ((in1 + in2) & Globals.calc_mask((uint)sizein))) ? 1 : 0;
    }
}
