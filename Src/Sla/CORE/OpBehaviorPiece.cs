using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorPiece : OpBehavior
    {
        public OpBehaviorPiece()
            : base(OpCode.CPUI_PIECE,false)
        {
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
            => (in1 << ((sizeout - sizein) * 8)) | in2;
    }
}
