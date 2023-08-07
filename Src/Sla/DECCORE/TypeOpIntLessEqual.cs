using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpIntLessEqual : TypeOpBinary
    {
        public TypeOpIntLessEqual(TypeFactory t)
            : base(t, OpCode.CPUI_INT_LESSEQUAL,"<=", type_metatype.TYPE_BOOL, type_metatype.TYPE_UINT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp::booloutput;
            addlflags = inherits_sign;
            behave = new OpBehaviorIntLessEqual();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntLessEqual(op);
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
