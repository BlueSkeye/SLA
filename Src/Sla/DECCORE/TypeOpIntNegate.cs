using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_NEGATE op-code
    internal class TypeOpIntNegate : TypeOpUnary
    {
        public TypeOpIntNegate(TypeFactory t)
            : base(t, OpCode.CPUI_INT_NEGATE,"~", type_metatype.TYPE_UINT, type_metatype.TYPE_UINT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = OperationType.logical_op | OperationType.inherits_sign;
            behave = new OpBehaviorIntNegate();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntNegate(op);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return castStrategy.arithmeticOutputStandard(op);
        }
    }
}
