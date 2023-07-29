using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_LESSEQUAL op-code
    internal class TypeOpFloatLessEqual : TypeOpBinary
    {
        public TypeOpFloatLessEqual(TypeFactory t, Translate trans)
            : base(t, CPUI_FLOAT_LESSEQUAL,"<=", TYPE_BOOL, TYPE_FLOAT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatLessEqual(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opFloatLessEqual(op);
        }
    }
}
