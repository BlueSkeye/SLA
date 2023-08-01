using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntSdiv : OpBehavior
    {
        public OpBehaviorIntSdiv()
            : base(OpCode.CPUI_INT_SDIV,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 == 0)
                throw new EvaluationError("Divide by 0");
            long num = (long)in1;     // Convert to signed
            long denom = (long)in2;
            Globals.sign_extend(ref num, 8 * sizein - 1);
            Globals.sign_extend(ref denom, 8 * sizein - 1);
            long sres = num / denom;    // Do the signed division
            Globals.zero_extend(ref sres, 8 * sizeout - 1); // Cut to appropriate size
            return (ulong)sres;     // Recast as unsigned
        }
    }
}
