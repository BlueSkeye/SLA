using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_NEG op-code
    internal class TypeOpFloatNeg : TypeOpUnary
    {
        public TypeOpFloatNeg(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_NEG,"-", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatNeg(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatNeg(op);
        }
    }
}
