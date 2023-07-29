using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_LESS op-code
    internal class TypeOpFloatLess : TypeOpBinary
    {
        public TypeOpFloatLess(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_LESS,"<", TYPE_BOOL, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatLess(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatLess(op);
        }
    }
}
