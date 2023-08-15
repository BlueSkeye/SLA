using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_NEG op-code
    internal class TypeOpFloatNeg : TypeOpUnary
    {
        public TypeOpFloatNeg(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_NEG,"-", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatNeg(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatNeg(op);
        }
    }
}
