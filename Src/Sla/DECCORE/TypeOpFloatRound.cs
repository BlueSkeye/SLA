using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_ROUND op-code
    internal class TypeOpFloatRound : TypeOpFunc
    {
        public TypeOpFloatRound(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_ROUND,"ROUND", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.unary;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatRound(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatRound(op);
        }
    }
}
