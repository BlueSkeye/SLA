using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleEarlyRemoval : Rule
    {
        public RuleEarlyRemoval(string g)
            : base(g, 0, "earlyremoval")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleEarlyRemoval(getGroup());
        }

        // This rule applies to all ops
        /// \class RuleEarlyRemoval
        /// \brief Get rid of unused PcodeOp objects where we can guarantee the output is unused
        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn;

            if (op.isCall()) return 0; // Functions automatically consumed
            if (op.isIndirectSource()) return 0;
            vn = op.getOut();
            if (vn == (Varnode)null) return 0;
            //  if (vn.isPersist()) return 0;
            if (!vn.hasNoDescend()) return 0;
            if (vn.isAutoLive()) return 0;
            AddrSpace* spc = vn.getSpace();
            if (spc.doesDeadcode())
                if (!data.deadRemovalAllowedSeen(spc))
                    return 0;

            data.opDestroy(op);     // Get rid of unused op
            return 1;
        }
    }
}
