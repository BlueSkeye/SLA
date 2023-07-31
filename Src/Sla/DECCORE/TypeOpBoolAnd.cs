using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_AND op-code
    internal class TypeOpBoolAnd : TypeOpBinary
    {
        public TypeOpBoolAnd(TypeFactory t)
            : base(t, OpCode.CPUI_BOOL_AND,"&&", type_metatype.TYPE_BOOL, type_metatype.TYPE_BOOL)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative | PcodeOp::booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolAnd();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBoolAnd(op);
        }
    }
}
