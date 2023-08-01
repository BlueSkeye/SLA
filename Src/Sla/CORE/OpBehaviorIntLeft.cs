using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntLeft : OpBehavior
    {
        public OpBehaviorIntLeft()
            : base(OpCode.CPUI_INT_LEFT,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            if (in2 >= (uint)sizeout * 8) {
                return 0;
            }
            ulong res = (in1 << (int)in2) & Globals.calc_mask((uint)sizeout);
            return res;
        }

        public override ulong recoverInputBinary(int slot, int sizeout, ulong @out, int sizein, ulong @in)
        {
            if ((slot != 0) || (@in >= (uint)sizeout * 8))
                return base.recoverInputBinary(slot, sizeout, @out, sizein, @in);
            int sa = (int)@in;
            if (((@out << (8 * sizeout - sa)) & Globals.calc_mask((uint)sizeout)) != 0)
                throw new EvaluationError("Output is not in range of left shift operation");
            return @out >> sa;
        }
    }
}
