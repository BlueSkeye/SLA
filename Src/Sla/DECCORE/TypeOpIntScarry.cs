using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SCARRY op-code
    internal class TypeOpIntScarry : TypeOpFunc
    {
        public TypeOpIntScarry(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SCARRY,"SCARRY", type_metatype.TYPE_BOOL, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary;
            behave = new OpBehaviorIntScarry();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntScarry(op);
        }

        public override string getOperatorName(PcodeOp op)
        {
            TextWriter s = new StringWriter();
            s.Write("{name}{op.getIn(0).getSize()}");
            return s.ToString();
        }
    }
}
