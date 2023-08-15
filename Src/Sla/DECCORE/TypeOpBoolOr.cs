using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BOOL_OR op-code
    internal class TypeOpBoolOr : TypeOpBinary
    {
        public TypeOpBoolOr(TypeFactory t)
            : base(t, OpCode.CPUI_BOOL_OR,"||", type_metatype.TYPE_BOOL, type_metatype.TYPE_BOOL)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.commutative | PcodeOp.Flags.booloutput;
            addlflags = logical_op;
            behave = new OpBehaviorBoolOr();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBoolOr(op);
        }
    }
}
