using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INSERT op-code
    internal class TypeOpInsert : TypeOpFunc
    {
        public TypeOpInsert(TypeFactory t)
            : base(t, OpCode.CPUI_INSERT,"INSERT", type_metatype.TYPE_UNKNOWN, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.ternary;
            // Dummy behavior
            behave = new OpBehavior(OpCode.CPUI_INSERT, false);
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            if (slot == 0)
                return tlst.getBase(op.getIn(slot).getSize(), type_metatype.TYPE_UNKNOWN);
            return base.getInputLocal(op, slot);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opInsertOp(op);
        }
    }
}
