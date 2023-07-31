using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the FLOAT_FLOAT2FLOAT op-code
    internal class TypeOpFloatFloat2Float : TypeOpFunc
    {
        public TypeOpFloatFloat2Float(TypeFactory t, Translate trans)
            : base(t, OpCode.CPUI_FLOAT_FLOAT2FLOAT,"FLOAT2FLOAT", type_metatype.TYPE_FLOAT, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp::unary;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatFloat2Float(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatFloat2Float(op);
        }
    }
}
