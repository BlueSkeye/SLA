using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_MULT op-code
    internal class TypeOpFloatMult : TypeOpBinary
    {
        public TypeOpFloatMult(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_MULT,"*", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.commutative;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatMult(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatMult(op);
        }
    }
}
