using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_OR op-code
    internal class TypeOpBoolOr : TypeOpBinary
    {
        public TypeOpBoolOr(TypeFactory t)
            : base(t, CPUI_BOOL_OR,"||", TYPE_BOOL, TYPE_BOOL)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative | PcodeOp::booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolOr();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opBoolOr(op);
        }
    }
}
