using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_ABS op-code
    internal class TypeOpFloatAbs : TypeOpFunc
    {
        public TypeOpFloatAbs(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_ABS,"ABS", TYPE_FLOAT, TYPE_FLOAT)
        {
            opflags = PcodeOp::unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatAbs(trans);
        }

        public void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatAbs(op);
        }
    }
}
