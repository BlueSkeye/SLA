﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_XOR op-code
    internal class TypeOpBoolXor : TypeOpBinary
    {
        public TypeOpBoolXor(TypeFactory t)
            : base(t, CPUI_BOOL_XOR,"^^", TYPE_BOOL, TYPE_BOOL)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative | PcodeOp::booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolXor();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opBoolXor(op);
        }
    }
}