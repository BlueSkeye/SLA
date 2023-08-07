﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_TRUNC op-code
    internal class TypeOpFloatTrunc : TypeOpFunc
    {
        public TypeOpFloatTrunc(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_TRUNC,"TRUNC", type_metatype.TYPE_INT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatTrunc(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatTrunc(op);
        }
    }
}
