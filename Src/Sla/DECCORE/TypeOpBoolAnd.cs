using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_AND op-code
    internal class TypeOpBoolAnd : TypeOpBinary
    {
        public TypeOpBoolAnd(TypeFactory t)
            : base(t, OpCode.CPUI_BOOL_AND,"&&", type_metatype.TYPE_BOOL, type_metatype.TYPE_BOOL)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.commutative | PcodeOp.Flags.booloutput;
            addlflags = OperationType.logical_op;
            behave = new OpBehaviorBoolAnd();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBoolAnd(op);
        }
    }
}
