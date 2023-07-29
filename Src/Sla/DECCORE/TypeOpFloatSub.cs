﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_SUB op-code
    internal class TypeOpFloatSub : TypeOpBinary
    {
        public TypeOpFloatSub(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_SUB,"-", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatSub(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatSub(op);
        }
    }
}