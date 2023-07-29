﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SBORROW op-code
    internal class TypeOpIntSborrow : TypeOpFunc
    {
        public TypeOpIntSborrow(TypeFactory t)
            : base(t, CPUI_INT_SBORROW,"SBORROW", TYPE_BOOL, TYPE_INT)
        {
            opflags = PcodeOp::binary;
            addlflags = arithmetic_op;
            behave = new OpBehaviorIntSborrow();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIntSborrow(op);
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;
            s << name << dec << op->getIn(0)->getSize();
            return s.str();
        }
    }
}