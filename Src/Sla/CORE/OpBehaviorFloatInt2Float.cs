using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorFloatInt2Float : OpBehavior
    {
        private readonly Translate translate; ///< Translate object for recovering float format
        
        public OpBehaviorFloatInt2Float(Translate trans)
            : base(OpCode.CPUI_FLOAT_INT2FLOAT,true)
        {
            translate = trans;
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
        {
            FloatFormat? format = translate.getFloatFormat(sizeout);
            if (format == (FloatFormat)null)
                return base.evaluateUnary(sizeout, sizein, in1);
            return format.opInt2Float(in1, sizein);
        }
    }
}
