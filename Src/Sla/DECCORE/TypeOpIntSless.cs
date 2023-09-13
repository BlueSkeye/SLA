using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SLESS op-code
    internal class TypeOpIntSless : TypeOpBinary
    {
        public TypeOpIntSless(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SLESS,"<", type_metatype.TYPE_BOOL, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.booloutput;
            addlflags = OperationType.inherits_sign;
            behave = new OpBehaviorIntSless();
        }

        public override Datatype? getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Datatype reqtype = op.inputTypeLocal(slot);
            if (castStrategy.checkIntPromotionForCompare(op, slot))
                return reqtype;
            Datatype curtype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, true, true);
        }

        public override Datatype? propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot == -1) || (outslot == -1))
                // Must propagate input <. input
                return (Datatype)null;
            if (alttype.getMetatype() != type_metatype.TYPE_INT)
                // Only propagate signed things
                return (Datatype)null;
            return alttype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSless(op);
        }
    }
}
