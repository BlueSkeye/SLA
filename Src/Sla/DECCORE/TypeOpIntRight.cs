using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_RIGHT op-code
    internal class TypeOpIntRight : TypeOpBinary
    {
        public TypeOpIntRight(TypeFactory t)
            : base(t, OpCode.CPUI_INT_RIGHT,">>", type_metatype.TYPE_UINT, type_metatype.TYPE_UINT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = OperationType.inherits_sign | OperationType.inherits_sign_zero
                | OperationType.shift_op;
            behave = new OpBehaviorIntRight();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntRight(op);
        }

        public override Datatype? getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            if (slot == 0) {
                Varnode vn = op.getIn(0);
                Datatype reqtype = op.inputTypeLocal(slot);
                Datatype curtype = vn.getHighTypeReadFacing(op);
                CastStrategy.IntPromotionCode promoType = castStrategy.intPromotionType(vn);
                if (   (promoType != CastStrategy.IntPromotionCode.NO_PROMOTION)
                    && ((promoType & CastStrategy.IntPromotionCode.UNSIGNED_EXTENSION) == 0))
                    return reqtype;
                return castStrategy.castStandard(reqtype, curtype, true, true);
            }
            return base.getInputCast(op, slot, castStrategy);
        }

        public override Datatype getInputLocal(PcodeOp op, int slot)
        {
            if (slot == 1)
                return tlst.getBaseNoChar(op.getIn(1).getSize(), type_metatype.TYPE_INT);
            return base.getInputLocal(op, slot);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Datatype res1 = op.getIn(0).getHighTypeReadFacing(op);
            if (res1.getMetatype() == type_metatype.TYPE_BOOL)
                res1 = tlst.getBase(res1.getSize(), type_metatype.TYPE_INT);
            return res1;
        }
    }
}
