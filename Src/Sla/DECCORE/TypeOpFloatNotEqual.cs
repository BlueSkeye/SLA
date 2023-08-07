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
            : base(t, OpCode.CPUI_FLOAT_NOTEQUAL,"!=", type_metatype.TYPE_BOOL, type_metatype.TYPE_FLOAT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp::booloutput | PcodeOp::commutative;
            addlflags = floatingpoint_op;
            behave = new OpBehaviorFloatNotEqual(trans);
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opFloatNotEqual(op);
        }
    }
}
