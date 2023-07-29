using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_LESS op-code
    internal class TypeOpIntLess : TypeOpBinary
    {
        public TypeOpIntLess(TypeFactory t)
            : base(t, CPUI_INT_LESS,"<", TYPE_BOOL, TYPE_UINT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput;
            addlflags = inherits_sign;
            behave = new OpBehaviorIntLess();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntLess(op);
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op.inputTypeLocal(slot);
            if (castStrategy.checkIntPromotionForCompare(op, slot))
                return reqtype;
            Datatype* curtype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, true, false);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            return TypeOpEqual::propagateAcrossCompare(alttype, tlst, invn, outvn, inslot, outslot);
        }
    }
}
