using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_EQUAL op-code
    internal class TypeOpFloatEqual : TypeOpBinary
    {
        public TypeOpFloatEqual(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_EQUAL,"==", TYPE_BOOL, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput | PcodeOp::commutative;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatEqual(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatEqual(op);
        }
    }
}
