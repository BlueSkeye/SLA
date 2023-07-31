using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_MULT op-code
    internal class TypeOpIntMult : TypeOpBinary
    {
        public TypeOpIntMult(TypeFactory t)
            : base(t, OpCode.CPUI_INT_MULT,"*", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative;
            addlflags = arithmetic_op | inherits_sign;
            behave = new OpBehaviorIntMult();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntMult(op);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return castStrategy.arithmeticOutputStandard(op);
        }
    }
}
