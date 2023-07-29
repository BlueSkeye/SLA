using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_NEGATE op-code
    internal class TypeOpBoolNegate : TypeOpUnary
    {
        public TypeOpBoolNegate(TypeFactory t)
            : base(t, CPUI_BOOL_NEGATE,"!", TYPE_BOOL, TYPE_BOOL)
        {
            opflags = PcodeOp::unary | PcodeOp::booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolNegate();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBoolNegate(op);
        }
    }
}
