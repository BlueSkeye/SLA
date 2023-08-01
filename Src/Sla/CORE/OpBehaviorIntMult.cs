using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntMult : OpBehavior
    {
        public OpBehaviorIntMult()
            : base(OpCode.CPUI_INT_MULT,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => (in1 * in2) & Globals.calc_mask((uint)sizeout);
    }
}
