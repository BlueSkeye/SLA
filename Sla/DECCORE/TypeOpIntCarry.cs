using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_CARRY op-code
    internal class TypeOpIntCarry : TypeOpFunc
    {
        public TypeOpIntCarry(TypeFactory t)
            : base(t, CPUI_INT_CARRY,"CARRY", TYPE_BOOL, TYPE_UINT)
        {
            opflags = PcodeOp::binary;
            addlflags = arithmetic_op;
            behave = new OpBehaviorIntCarry();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opIntCarry(op);
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;
            s << name << dec << op->getIn(0)->getSize();
            return s.str();
        }
    }
}
