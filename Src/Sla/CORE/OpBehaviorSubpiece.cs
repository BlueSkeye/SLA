using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorSubpiece : OpBehavior
    {
        public OpBehaviorSubpiece()
            : base(OpCode.CPUI_SUBPIECE,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => (in1 >> ((int)in2 * 8)) & Globals.calc_mask((uint)sizeout);
    }
}
