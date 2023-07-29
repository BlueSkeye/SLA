using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SUB op-code
    internal class TypeOpIntSub : TypeOpBinary
    {
        public TypeOpIntSub(TypeFactory t)
            : base(t, CPUI_INT_SUB,"-", TYPE_INT, TYPE_INT)
        {
            opflags = PcodeOp::binary;
            addlflags = arithmetic_op | inherits_sign;
            behave = new OpBehaviorIntSub();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSub(op);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return castStrategy.arithmeticOutputStandard(op);  // Use arithmetic typing rules
        }
    }
}
