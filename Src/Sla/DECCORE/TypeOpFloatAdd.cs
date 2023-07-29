using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_ADD op-code
    internal class TypeOpFloatAdd : TypeOpBinary
    {
        public TypeOpFloatAdd(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_ADD,"+", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatAdd(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatAdd(op);
        }
    }
}
