using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_AND op-code
    internal class TypeOpBoolAnd : TypeOpBinary
    {
        public TypeOpBoolAnd(TypeFactory t)
            : base(t, CPUI_BOOL_AND,"&&", TYPE_BOOL, TYPE_BOOL)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative | PcodeOp::booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolAnd();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opBoolAnd(op);
        }
    }
}
