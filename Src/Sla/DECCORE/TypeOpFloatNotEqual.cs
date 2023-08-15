using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_NOTEQUAL op-code
    internal class TypeOpFloatNotEqual : TypeOpBinary
    {
        public TypeOpFloatNotEqual(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_NOTEQUAL,"!=", type_metatype.TYPE_BOOL, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.booloutput | PcodeOp.Flags.commutative;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatNotEqual(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatNotEqual(op);
        }
    }
}
