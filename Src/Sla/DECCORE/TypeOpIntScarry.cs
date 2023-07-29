using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_SCARRY op-code
    internal class TypeOpIntScarry : TypeOpFunc
    {
        public TypeOpIntScarry(TypeFactory t)
            : base(t, CPUI_INT_SCARRY,"SCARRY", TYPE_BOOL, TYPE_INT)
        {
            opflags = PcodeOp::binary;
            behave = new OpBehaviorIntScarry();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIntScarry(op);
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;
            s << name << dec << op->getIn(0)->getSize();
            return s.str();
        }
    }
}
