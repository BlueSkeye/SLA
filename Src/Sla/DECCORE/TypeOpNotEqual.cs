using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_NOTEQUAL op-code
    internal class TypeOpNotEqual : TypeOpBinary
    {
        public TypeOpNotEqual(TypeFactory t)
            : base(t, CPUI_INT_NOTEQUAL, "!=", TYPE_BOOL, TYPE_INT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput | PcodeOp::commutative;
            addlflags = inherits_sign;
            behave = new OpBehaviorNotEqual();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIntNotEqual(op);
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op->getIn(0)->getHighTypeReadFacing(op);    // Input arguments should be the same type
            Datatype* othertype = op->getIn(1)->getHighTypeReadFacing(op);
            if (0 > othertype->typeOrder(*reqtype))
                reqtype = othertype;
            if (castStrategy->checkIntPromotionForCompare(op, slot))
                return reqtype;
            othertype = op->getIn(slot)->getHighTypeReadFacing(op);
            return castStrategy->castStandard(reqtype, othertype, false, false);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            return TypeOpEqual::propagateAcrossCompare(alttype, tlst, invn, outvn, inslot, outslot);
        }
    }
}
