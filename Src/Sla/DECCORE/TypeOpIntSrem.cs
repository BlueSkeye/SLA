using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SREM op-code
    internal class TypeOpIntSrem : TypeOpBinary
    {
        public TypeOpIntSrem(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SREM,"%", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary;
            addlflags = arithmetic_op | inherits_sign | inherits_sign_zero;
            behave = new OpBehaviorIntSrem();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSrem(op);
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Varnode vn = op.getIn(slot);
            Datatype* reqtype = op.inputTypeLocal(slot);
            Datatype* curtype = vn.getHighTypeReadFacing(op);
            int promoType = castStrategy.intPromotionType(vn);
            if (promoType != CastStrategy::NO_PROMOTION && ((promoType & CastStrategy::SIGNED_EXTENSION) == 0))
                return reqtype;
            return castStrategy.castStandard(reqtype, curtype, true, true);
        }
    }
}
