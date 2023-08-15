using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_LESS op-code
    internal class TypeOpFloatLess : TypeOpBinary
    {
        public TypeOpFloatLess(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_LESS,"<", type_metatype.TYPE_BOOL, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.booloutput;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatLess(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatLess(op);
        }
    }
}
