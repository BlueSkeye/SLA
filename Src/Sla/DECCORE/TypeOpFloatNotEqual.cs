using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_NOTEQUAL op-code
    internal class TypeOpFloatNotEqual : TypeOpBinary
    {
        public TypeOpFloatNotEqual(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_NOTEQUAL,"!=", TYPE_BOOL, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput | PcodeOp::commutative;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatNotEqual(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatNotEqual(op);
        }
    }
}
