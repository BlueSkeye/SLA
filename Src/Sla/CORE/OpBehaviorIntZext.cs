using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntZext : OpBehavior
    {
        public OpBehaviorIntZext()
            : base(OpCode.CPUI_INT_ZEXT,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1) => in1;
        
        public override ulong recoverInputUnary(int sizeout, ulong @out, int sizein)
        {
            ulong mask = Globals.calc_mask((uint)sizein);
            if ((mask &@out)!=@out)
                throw new EvaluationError("Output is not in range of zext operation");
            return @out;
        }
    }
}
