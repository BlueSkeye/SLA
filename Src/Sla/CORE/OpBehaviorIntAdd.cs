using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorIntAdd : OpBehavior
    {
        public OpBehaviorIntAdd()
            : base(OpCode.CPUI_INT_ADD,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => (in1 + in2) & Globals.calc_mask((uint)sizeout);

        public override ulong recoverInputBinary(int slot, int sizeout, ulong @out, int sizein, ulong @in)
            => (@out-@in) &Globals.calc_mask((uint)sizeout);
    }
}
