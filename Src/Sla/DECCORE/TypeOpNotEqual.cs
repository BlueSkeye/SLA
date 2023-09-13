using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_NOTEQUAL op-code
    internal class TypeOpNotEqual : TypeOpBinary
    {
        public TypeOpNotEqual(TypeFactory t)
            : base(t, OpCode.CPUI_INT_NOTEQUAL, "!=", type_metatype.TYPE_BOOL, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.booloutput | PcodeOp.Flags.commutative;
            addlflags = OperationType.inherits_sign;
            behave = new OpBehaviorNotEqual();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntNotEqual(op);
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            // Input arguments should be the same type
            Datatype reqtype = op.getIn(0).getHighTypeReadFacing(op);
            Datatype othertype = op.getIn(1).getHighTypeReadFacing(op);
            if (0 > othertype.typeOrder(reqtype))
                reqtype = othertype;
            if (castStrategy.checkIntPromotionForCompare(op, slot))
                return reqtype;
            othertype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, othertype, false, false);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn,
            Varnode outvn, int inslot, int outslot)
        {
            return TypeOpEqual.propagateAcrossCompare(alttype, tlst, invn, outvn, inslot,
                outslot);
        }
    }
}
