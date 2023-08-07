using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_2COMP op-code
    internal class TypeOpInt2Comp : TypeOpUnary
    {
        public TypeOpInt2Comp(TypeFactory t)
            : base(t, OpCode.CPUI_INT_2COMP,"-", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = arithmetic_op | inherits_sign;
            behave = new OpBehaviorInt2Comp();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opInt2Comp(op);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return castStrategy.arithmeticOutputStandard(op);
        }
    }
}
