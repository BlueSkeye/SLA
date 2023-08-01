using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntNegate : OpBehavior
    {
        public OpBehaviorIntNegate()
            : base(OpCode.CPUI_INT_NEGATE,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
            => Globals.uintb_negate(in1, sizein)

        public override ulong recoverInputUnary(int sizeout, ulong @out, int sizein)
            => Globals.uintb_negate(@out, sizein);
    }
}
