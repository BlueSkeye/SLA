using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorPopcount : OpBehavior
    {
        public OpBehaviorPopcount()
            : base(OpCode.CPUI_POPCOUNT,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1) => (ulong)Globals.popcount(in1);
    }
}
