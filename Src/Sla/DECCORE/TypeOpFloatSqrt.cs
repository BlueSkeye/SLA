using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_SQRT op-code
    internal class TypeOpFloatSqrt : TypeOpFunc
    {
        public TypeOpFloatSqrt(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_SQRT,"SQRT", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatSqrt(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatSqrt(op);
        }
    }
}
