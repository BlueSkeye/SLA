using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntRem : OpBehavior
    {
        public OpBehaviorIntRem()
            : base(OpCode.CPUI_INT_REM,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 == 0)
                throw new EvaluationError("Remainder by 0");

            ulong res = in1 % in2;
            return res;
        }
    }
}
