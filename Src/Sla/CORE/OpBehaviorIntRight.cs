using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntRight : OpBehavior
    {
        public OpBehaviorIntRight()
            : base(OpCode.CPUI_INT_RIGHT,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 >= (uint)sizeout * 8)
            {
                return 0;
            }
            ulong res = (in1 & Globals.calc_mask((uint)sizeout)) >> (int)in2;
            return res;
        }

        public override ulong recoverInputBinary(int slot, int sizeout, ulong @out, int sizein, ulong @in)
        {
            if ((slot != 0) || (@in >= (uint)sizeout * 8))
                return base.recoverInputBinary(slot, sizeout, @out, sizein, @in);
            int sa = (int)@in;
            if ((@out >> (8 * sizein - sa)) != 0)
                throw new EvaluationError("Output is not in range of right shift operation");
            return @out << sa;
        }
    }
}
