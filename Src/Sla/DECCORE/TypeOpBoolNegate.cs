using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_NEGATE op-code
    internal class TypeOpBoolNegate : TypeOpUnary
    {
        public TypeOpBoolNegate(TypeFactory t)
            : base(t, OpCode.CPUI_BOOL_NEGATE,"!", type_metatype.TYPE_BOOL, type_metatype.TYPE_BOOL)
        {
            opflags = PcodeOp.Flags.unary | PcodeOp.Flags.booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolNegate();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBoolNegate(op);
        }
    }
}
