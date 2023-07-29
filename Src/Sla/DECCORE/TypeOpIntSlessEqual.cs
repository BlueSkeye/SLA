using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpIntSlessEqual : TypeOpBinary
    {
        public TypeOpIntSlessEqual(TypeFactory t)
            : base(t, CPUI_INT_SLESSEQUAL,"<=", TYPE_BOOL, TYPE_INT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput;
            addlflags = inherits_sign;
            behave = new OpBehaviorIntSlessEqual();
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op.inputTypeLocal(slot);
            if (castStrategy.checkIntPromotionForCompare(op, slot))
                return reqtype;
            Datatype* curtype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, true, true);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            if ((inslot == -1) || (outslot == -1)) return (Datatype*)0; // Must propagate input <. input
            if (alttype.getMetatype() != TYPE_INT) return (Datatype*)0;    // Only propagate signed things
            return alttype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSlessEqual(op);
        }
    }
}
