using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorLzcount : OpBehavior
    {
        public OpBehaviorLzcount()
            : base(OpCode.CPUI_LZCOUNT,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
        {
            return (ulong)(Globals.count_leading_zeros(in1) - 8 * (sizeof(ulong) - sizein));
        }
    }
}
