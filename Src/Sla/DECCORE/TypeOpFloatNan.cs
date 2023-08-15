using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_NAN op-code
    internal class TypeOpFloatNan : TypeOpFunc
    {
        public TypeOpFloatNan(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_NAN,"NAN", type_metatype.TYPE_BOOL, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary | PcodeOp.Flags.booloutput;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatNan(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatNan(op);
        }
    }
}
