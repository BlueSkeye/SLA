using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SBORROW op-code
    internal class TypeOpIntSborrow : TypeOpFunc
    {
        public TypeOpIntSborrow(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SBORROW,"SBORROW", type_metatype.TYPE_BOOL, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = OperationType.arithmetic_op;
            behave = new OpBehaviorIntSborrow();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSborrow(op);
        }

        public override string getOperatorName(PcodeOp op)
        {
            TextWriter s = new StringWriter();
            s.Write($"{name}{op.getIn(0).getSize()}");
            return s.ToString();
        }
    }
}
