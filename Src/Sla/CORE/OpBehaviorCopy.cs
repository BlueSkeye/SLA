using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorCopy : OpBehavior
    {
        public OpBehaviorCopy()
            : base(OpCode.CPUI_COPY,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1) => in1;

        public override ulong recoverInputUnary(int sizeout, ulong @out, int sizein) => @out;
    }
}
