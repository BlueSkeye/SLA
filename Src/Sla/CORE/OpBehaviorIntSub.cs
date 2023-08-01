using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntSub : OpBehavior
    {
        public OpBehaviorIntSub()
            : base(OpCode.CPUI_INT_SUB,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            ulong res = (in1 - in2) & Globals.calc_mask((uint)sizeout);
            return res;
        }

        public override ulong recoverInputBinary(int slot, int sizeout, ulong @out, int sizein, ulong @in)
        {
            ulong res;
            if (slot == 0)
                res = @in + @out;
            else
                res = @in - @out;
            res &= Globals.calc_mask((uint)sizeout);
            return res;
        }
    }
}
