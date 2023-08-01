using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntDiv : OpBehavior
    {
        public OpBehaviorIntDiv()
            : base(OpCode.CPUI_INT_DIV,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 == 0)
                throw new EvaluationError("Divide by 0");
            return in1 / in2;
        }
    }
}
