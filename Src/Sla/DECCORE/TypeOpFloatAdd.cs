using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_ADD op-code
    internal class TypeOpFloatAdd : TypeOpBinary
    {
        public TypeOpFloatAdd(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_ADD,"+", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.commutative;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatAdd(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatAdd(op);
        }
    }
}
