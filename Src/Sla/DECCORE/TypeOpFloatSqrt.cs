using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_SQRT op-code
    internal class TypeOpFloatSqrt : TypeOpFunc
    {
        public TypeOpFloatSqrt(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_SQRT,"SQRT", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatSqrt(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatSqrt(op);
        }
    }
}
