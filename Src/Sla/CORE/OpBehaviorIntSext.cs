using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntSext : OpBehavior
    {
        public OpBehaviorIntSext()
            : base(OpCode.CPUI_INT_SEXT,true)
        {
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
        {
            ulong res = Globals.sign_extend(in1, sizein, sizeout);
            return res;
        }

        public override ulong recoverInputUnary(int sizeout, ulong @out, int sizein)
        {
            ulong masklong = Globals.calc_mask((uint)sizeout);
            ulong maskshort = Globals.calc_mask((uint)sizein);

            if ((@out &(maskshort ^ (maskshort >> 1))) == 0) { // Positive input
                if ((@out &maskshort) != @out)
                    throw new EvaluationError("Output is not in range of sext operation");
            }
            else {
                // Negative input
                if ((@out &(masklong ^ maskshort)) != (masklong ^ maskshort))
                    throw new EvaluationError("Output is not in range of sext operation");
            }
            return (@out&maskshort);
        }
    }
}
