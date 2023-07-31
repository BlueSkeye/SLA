using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpCast : TypeOp
    {
        public TypeOpCast(TypeFactory t)
            : base(t, OpCode.CPUI_CAST,"(cast)")

        {
            opflags = PcodeOp::unary | PcodeOp::special | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_CAST, false, true); // Dummy behavior
        }

        // We don't care what types are cast
        // So no input and output requirements
        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opCast(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode::printRaw(s, op.getOut());
            s << " = " << name << ' ';
            Varnode::printRaw(s, op.getIn(0));
        }
    }
}
