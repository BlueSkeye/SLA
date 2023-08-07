using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SEXT op-code
    internal class TypeOpIntSext : TypeOpFunc
    {
        public TypeOpIntSext(TypeFactory t)
            : base(t, OpCode.CPUI_INT_SEXT,"SEXT", type_metatype.TYPE_INT, type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.unary;
            behave = new OpBehaviorIntSext();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntSext(op, readOp);
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;

            s << name << dec << op.getIn(0).getSize() << op.getOut().getSize();
            return s.str();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op.inputTypeLocal(slot);
            if (castStrategy.checkIntPromotionForExtension(op))
                return reqtype;
            Datatype* curtype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, curtype, true, false);
        }
    }
}
