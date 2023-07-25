using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_ZEXT op-code
    internal class TypeOpIntZext : TypeOpFunc
    {
        public TypeOpIntZext(TypeFactory t)
            : base(t, CPUI_INT_ZEXT,"ZEXT", TYPE_UINT, TYPE_UINT)
        {
            opflags = PcodeOp::unary;
            behave = new OpBehaviorIntZext();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIntZext(op, readOp);
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;

            s << name << dec << op->getIn(0)->getSize() << op->getOut()->getSize();
            return s.str();
        }

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op->inputTypeLocal(slot);
            if (castStrategy->checkIntPromotionForExtension(op))
                return reqtype;
            Datatype* curtype = op->getIn(slot)->getHighTypeReadFacing(op);
            return castStrategy->castStandard(reqtype, curtype, true, false);
        }
    }
}
