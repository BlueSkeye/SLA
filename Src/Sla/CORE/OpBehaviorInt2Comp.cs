using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorInt2Comp : OpBehavior
    {
        public OpBehaviorInt2Comp()
            : base(OpCode.CPUI_INT_2COMP,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
            => Globals.uintb_negate(in1 - 1, sizein);

        public override ulong recoverInputUnary(int sizeout, ulong @out, int sizein)
            => Globals.uintb_negate(@out -1, sizein);
    }
}
