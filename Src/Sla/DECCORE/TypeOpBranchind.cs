using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the BRANCHIND op-code
    internal class TypeOpBranchind : TypeOp
    {
        public TypeOpBranchind(TypeFactory t)
        {
            opflags = PcodeOp::special | PcodeOp::branch | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_BRANCHIND, false, true); // Dummy behavior
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opBranchind(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s << name << ' ';
            Varnode::printRaw(s, op.getIn(0));
        }
    }
}
