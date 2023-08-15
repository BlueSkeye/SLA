using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_CEIL op-code
    internal class TypeOpFloatCeil : TypeOpFunc
    {
        public TypeOpFloatCeil(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_CEIL,"CEIL", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatCeil(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatCeil(op);
        }
    }
}
