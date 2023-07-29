using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BRANCH op-code
    internal class TypeOpBranch : TypeOp
    {
        public TypeOpBranch(TypeFactory t)
        {
            opflags = (PcodeOp::special | PcodeOp::branch | PcodeOp::coderef | PcodeOp::nocollapse);
            behave = new OpBehavior(CPUI_BRANCH, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBranch(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s << name << ' ';
            Varnode::printRaw(s, op.getIn(0));
        }
    }
}
