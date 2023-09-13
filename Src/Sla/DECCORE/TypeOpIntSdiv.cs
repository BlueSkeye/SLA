using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SDIV op-code
    internal class TypeOpIntSdiv : TypeOpBinary
    {
        public TypeOpIntSdiv(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SDIV,"/", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = OperationType.arithmetic_op | OperationType.inherits_sign;
            behave = new OpBehaviorIntSdiv();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSdiv(op);
        }

        public override Datatype? getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Varnode vn = op.getIn(slot);
            Datatype reqtype = op.inputTypeLocal(slot);
            Datatype curtype = vn.getHighTypeReadFacing(op);
            CastStrategy.IntPromotionCode promoType = castStrategy.intPromotionType(vn);
            if (   (promoType != CastStrategy.IntPromotionCode.NO_PROMOTION)
                && ((promoType & CastStrategy.IntPromotionCode.SIGNED_EXTENSION) == 0))
                return reqtype;
            return castStrategy.castStandard(reqtype, curtype, true, true);
        }
    }
}
