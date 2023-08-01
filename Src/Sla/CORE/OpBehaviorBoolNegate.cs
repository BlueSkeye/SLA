using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorBoolNegate : OpBehavior
    {
        public OpBehaviorBoolNegate()
            : base(OpCode.CPUI_BOOL_NEGATE,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1) => in1 ^ 1;
    }
}
