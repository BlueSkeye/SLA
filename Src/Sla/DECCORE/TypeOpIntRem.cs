using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_REM op-code
    internal class TypeOpIntRem : TypeOpBinary
    {
        public TypeOpIntRem(TypeFactory t)
            : base(t, OpCode.CPUI_INT_REM,"%", type_metatype.TYPE_UINT, type_metatype.TYPE_UINT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = OperationType.arithmetic_op | OperationType.inherits_sign
                | OperationType.inherits_sign_zero;
            behave = new OpBehaviorIntRem();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntRem(op);
        }

        public override Datatype? getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Varnode vn = op.getIn(slot);
            Datatype reqtype = op.inputTypeLocal(slot);
            Datatype curtype = vn.getHighTypeReadFacing(op);
            CastStrategy.IntPromotionCode promoType = castStrategy.intPromotionType(vn);
            if (   (promoType != CastStrategy.IntPromotionCode.NO_PROMOTION)
                && ((promoType & CastStrategy.IntPromotionCode.UNSIGNED_EXTENSION) == 0))
                return reqtype;
            return castStrategy.castStandard(reqtype, curtype, true, true);
        }
    }
}
