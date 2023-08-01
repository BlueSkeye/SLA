using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntSrem : OpBehavior
    {
        public OpBehaviorIntSrem()
            : base(OpCode.CPUI_INT_SREM,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 == 0)
                throw new EvaluationError("Remainder by 0");
            long val = (long)in1;
            long mod = (long)in2;
            Globals.sign_extend(ref val, 8 * sizein - 1);   // Convert inputs to signed values
            Globals.sign_extend(ref mod, 8 * sizein - 1);
            long sres = val % mod;  // Do the remainder
            Globals.zero_extend(ref sres, 8 * sizeout - 1); // Convert back to unsigned
            return (ulong)sres;
        }
    }
}
