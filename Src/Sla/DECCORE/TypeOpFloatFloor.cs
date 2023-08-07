﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_FLOOR op-code
    internal class TypeOpFloatFloor : TypeOpFunc
    {
        public TypeOpFloatFloor(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_FLOOR,"FLOOR", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatFloor(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatFloor(op);
        }
    }
}
