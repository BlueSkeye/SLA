﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class OpBehaviorFloatNan : OpBehavior
    {
        private readonly Translate translate; ///< Translate object for recovering float format
        
        public OpBehaviorFloatNan(Translate trans)
            : base(OpCode.CPUI_FLOAT_NAN,true)
        {
            translate = trans;
        }

        public override ulong evaluateUnary(int sizeout, int sizein, ulong in1)
        {
            FloatFormat? format = translate.getFloatFormat(sizein);
            if (format == (FloatFormat)null)
                return base.evaluateUnary(sizeout, sizein, in1);
            return format.opNan(in1);
        }
    }
}