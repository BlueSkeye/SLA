using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_MULT op-code
    internal class TypeOpFloatMult : TypeOpBinary
    {
        public TypeOpFloatMult(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_MULT,"*", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatMult(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatMult(op);
        }
    }
}
