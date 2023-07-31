using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_DIV op-code
    internal class TypeOpFloatDiv : TypeOpBinary
    {
        public TypeOpFloatDiv(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_DIV,"/", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp::binary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatDiv(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatDiv(op);
        }
    }
}
