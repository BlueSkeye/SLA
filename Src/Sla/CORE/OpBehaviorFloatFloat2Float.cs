using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorFloatFloat2Float : OpBehavior
    {
        private readonly Translate translate; ///< Translate object for recovering float format
        
        public OpBehaviorFloatFloat2Float(Translate trans)
            : base(OpCode.CPUI_FLOAT_FLOAT2FLOAT,true)
        {
            translate = trans;
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
        {
            FloatFormat? formatout = translate.getFloatFormat(sizeout);
            if (formatout == (FloatFormat)null)
                return base.evaluateUnary(sizeout, sizein, in1);
            FloatFormat? formatin = translate.getFloatFormat(sizein);
            if (formatin == (FloatFormat)null)
                return base.evaluateUnary(sizeout, sizein, in1);
            return formatin.opFloat2Float(in1, formatout);
        }
    }
}
