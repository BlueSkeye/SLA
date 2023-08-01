using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorBoolOr : OpBehavior
    {
        public OpBehaviorBoolOr()
            : base(OpCode.CPUI_BOOL_OR,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2) => in1 | in2;
    }
}
