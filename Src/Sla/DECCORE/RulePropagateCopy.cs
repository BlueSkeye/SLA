using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePropagateCopy : Rule
    {
        public RulePropagateCopy(string g)
            : base(g, 0, "propagatecopy")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePropagateCopy(getGroup());
        }

        // applies to all opcodes
        /// \class RulePropagateCopy
        /// \brief Propagate the input of a COPY to all the places that read the output
        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            int i;
            PcodeOp copyop;
            Varnode vn, invn;

            if (op.stopsCopyPropagation()) return 0;
            for (i = 0; i < op.numInput(); ++i) {
                vn = op.getIn(i);
                if (!vn.isWritten()) continue; // Varnode must be written to

                copyop = vn.getDef() ?? throw new BugException();
                if (copyop.code() != OpCode.CPUI_COPY)
                    continue;           // not a propagating instruction

                invn = copyop.getIn(0);
                if (!invn.isHeritageKnown()) continue; // Don't propagate free's away from their first use
                if (invn == vn)
                    throw new LowlevelError("Self-defined varnode");
                if (op.isMarker()) {
                    if (invn.isConstant()) continue;       // Don't propagate constants into markers
                    if (vn.isAddrForce()) continue;        // Don't propagate if we are keeping the COPY anyway
                    if (invn.isAddrTied() && op.getOut().isAddrTied() &&
                    (op.getOut().getAddr() != invn.getAddr()))
                        continue;       // We must not allow merging of different addrtieds
                }
                data.opSetInput(op, invn, i); // otherwise propagate just a single copy
                return 1;
            }
            return 0;
        }
    }
}
