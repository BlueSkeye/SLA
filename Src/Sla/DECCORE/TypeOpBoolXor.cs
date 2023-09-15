using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_XOR op-code
    internal class TypeOpBoolXor : TypeOpBinary
    {
        public TypeOpBoolXor(TypeFactory t)
            : base(t, OpCode.CPUI_BOOL_XOR,"^^", type_metatype.TYPE_BOOL, type_metatype.TYPE_BOOL)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.commutative | PcodeOp.Flags.booloutput;
            addlflags = OperationType.logical_op;
            behave = new OpBehaviorBoolXor();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBoolXor(op);
        }
    }
}
