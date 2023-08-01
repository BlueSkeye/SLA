using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorFloatLessEqual : OpBehavior
    {
        private readonly Translate translate; ///< Translate object for recovering float format
        
        public OpBehaviorFloatLessEqual(Translate trans)
            : base(OpCode.CPUI_FLOAT_LESSEQUAL,false)
        {
            translate = trans;
        }

        public override ulong evaluateBinary(int sizeout, int sizein, ulong in1, ulong in2)
        {
            FloatFormat? format = translate.getFloatFormat(sizein);
            if (format == (FloatFormat)null)
                return base.evaluateBinary(sizeout, sizein, in1, in2);
            return format.opLessEqual(in1, in2);
        }
    }
}
