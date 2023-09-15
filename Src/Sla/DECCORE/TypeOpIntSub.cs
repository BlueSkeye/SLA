using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SUB op-code
    internal class TypeOpIntSub : TypeOpBinary
    {
        public TypeOpIntSub(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SUB,"-", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = OperationType.arithmetic_op | OperationType.inherits_sign;
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
