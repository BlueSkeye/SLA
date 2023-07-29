using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SLESS op-code
    internal class TypeOpIntSless : TypeOpBinary
    {
        public TypeOpIntSless(TypeFactory t)
            : base(t, CPUI_INT_SLESS,"<", TYPE_BOOL, TYPE_INT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput;
            addlflags = inherits_sign;
            behave = new OpBehaviorIntSless();
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op->inputTypeLocal(slot);
            if (castStrategy->checkIntPromotionForCompare(op, slot))
                return reqtype;
            Datatype* curtype = op->getIn(slot)->getHighTypeReadFacing(op);
            return castStrategy->castStandard(reqtype, curtype, true, true);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            if ((inslot == -1) || (outslot == -1)) return (Datatype*)0; // Must propagate input <-> input
            if (alttype->getMetatype() != TYPE_INT) return (Datatype*)0;    // Only propagate signed things
            return alttype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIntSless(op);
        }
    }
}
