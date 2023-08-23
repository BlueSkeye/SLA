using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_CARRY op-code
    internal class TypeOpIntCarry : TypeOpFunc
    {
        public TypeOpIntCarry(TypeFactory t)
            : base(t, OpCode.CPUI_INT_CARRY,"CARRY", type_metatype.TYPE_BOOL, type_metatype.TYPE_UINT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = OperationType.arithmetic_op;
            behave = new OpBehaviorIntCarry();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntCarry(op);
        }

        public override string getOperatorName(PcodeOp op)
        {
            TextWriter s = new StringWriter();
            s.Write($"{name}{op.getIn(0).getSize()}");
            return s.ToString();
        }
    }
}
