using Sla.CORE;
using Sla.DECCORE;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_EQUAL op-code
    internal class TypeOpFloatEqual : TypeOpBinary
    {
        public TypeOpFloatEqual(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_EQUAL,"==", type_metatype.TYPE_BOOL, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.booloutput | PcodeOp.Flags.commutative;
            addlflags = OperationType.floatingpoint_op;
            behave = new OpBehaviorFloatEqual(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatEqual(op);
        }
    }
}
