using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SRIGHT op-code
    internal class TypeOpIntSright : TypeOpBinary
    {
        public TypeOpIntSright(TypeFactory t)
            : base(t, CPUI_INT_SRIGHT,">>", TYPE_INT, TYPE_INT)
        {
            opflags = PcodeOp::binary;
            addlflags = inherits_sign | inherits_sign_zero | shift_op;
            behave = new OpBehaviorIntSright();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSright(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode::printRaw(s, op.getOut());
            s << " = ";
            Varnode::printRaw(s, op.getIn(0));
            s << " s>> ";
            Varnode::printRaw(s, op.getIn(1));
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            if (slot == 0)
            {
                Varnode vn = op.getIn(0);
                Datatype* reqtype = op.inputTypeLocal(slot);
                Datatype* curtype = vn.getHighTypeReadFacing(op);
                int4 promoType = castStrategy.intPromotionType(vn);
                if (promoType != CastStrategy::NO_PROMOTION && ((promoType & CastStrategy::SIGNED_EXTENSION) == 0))
                    return reqtype;
                return castStrategy.castStandard(reqtype, curtype, true, true);
            }
            return TypeOpBinary::getInputCast(op, slot, castStrategy);
        }

        public override Datatype getInputLocal(PcodeOp op, int4 slot)
        {
            if (slot == 1)
                return tlst.getBaseNoChar(op.getIn(1).getSize(), TYPE_INT);
            return TypeOpBinary::getInputLocal(op, slot);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Datatype* res1 = op.getIn(0).getHighTypeReadFacing(op);
            if (res1.getMetatype() == TYPE_BOOL)
                res1 = tlst.getBase(res1.getSize(), TYPE_INT);
            return res1;
        }
    }
}
