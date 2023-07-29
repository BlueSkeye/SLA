using ghidra;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_ROUND op-code
    internal class TypeOpFloatRound : TypeOpFunc
    {
        public TypeOpFloatRound(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_ROUND,"ROUND", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatRound(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatRound(op);
        }
    }
}
