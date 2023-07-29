using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Remove conditional branches if the condition is constant
    internal class ActionDeterminedBranch : Action
    {
        public ActionDeterminedBranch(string g)
            : base(0,"determinedbranch", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDeterminedBranch(getGroup());
        }

        public override int apply(Funcdata data)
        {
            int i;
            BlockGraph graph = data.getBasicBlocks();
            BlockBasic* bb;
            PcodeOp* cbranch;

            for (i = 0; i < graph.getSize(); ++i)
            {
                bb = (BlockBasic*)graph.getBlock(i);
                cbranch = bb.lastOp();
                if ((cbranch == (PcodeOp*)0) || (cbranch.code() != CPUI_CBRANCH)) continue;
                if (!cbranch.getIn(1).isConstant()) continue;
                ulong val = cbranch.getIn(1).getOffset();
                int num = ((val != 0) != cbranch.isBooleanFlip()) ? 0 : 1;
                data.removeBranch(bb, num);
                count += 1;
            }
            return 0;
        }
    }
}
