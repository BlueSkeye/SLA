using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntSright : OpBehavior
    {
        public OpBehaviorIntSright()
            : base(OpCode.CPUI_INT_SRIGHT,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 >= (ulong)(8 * sizeout)) {
                return Globals.signbit_negative(in1, sizein) ? Globals.calc_mask((uint)sizeout) : 0;
            }

            ulong res;
            if (Globals.signbit_negative(in1, sizein)) {
                res = in1 >> (int)in2;
                ulong mask = Globals.calc_mask((uint)sizein);
                mask = (mask >> (int)in2) ^ mask;
                res |= mask;
            }
            else {
                res = in1 >> (int)in2;
            }
            return res;
        }

        public override ulong recoverInputBinary(int slot, int sizeout, ulong @out, int sizein, ulong @in)
        {
            if ((slot != 0) || (@in >= (uint)sizeout * 8))
                return base.recoverInputBinary(slot, sizeout, @out, sizein, @in);

            int sa = (int)@in;
            ulong testval = @out>> (sizein * 8 - sa - 1);
            int count = 0;
            for (int i = 0; i <= sa; ++i) {
                if ((testval & 1) != 0) count += 1;
                testval >>= 1;
            }
            if (count != sa + 1)
                throw new EvaluationError("Output is not in range of right shift operation");
            return @out<< sa;
        }
    }
}
