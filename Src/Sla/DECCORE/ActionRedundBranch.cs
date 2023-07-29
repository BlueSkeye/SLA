using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Get rid of \b redundant branches: duplicate edges between the same input and output block
    internal class ActionRedundBranch : Action
    {
        public ActionRedundBranch(string g)
            : base(0,"redundbranch", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionRedundBranch(getGroup());
        }

        public override int apply(Funcdata data)
        {
            // Remove redundant branches, i.e. a CPUI_CBRANCH that falls thru and branches to the same place
            int4 i, j;
            BlockGraph graph = data.getBasicBlocks();
            BlockBasic* bb;
            FlowBlock* bl;

            for (i = 0; i < graph.getSize(); ++i)
            {
                bb = (BlockBasic*)graph.getBlock(i);
                if (bb->sizeOut() == 0) continue;
                bl = bb->getOut(0);
                if (bb->sizeOut() == 1)
                {
                    if ((bl->sizeIn() == 1) && (!bl->isEntryPoint()) && (!bb->isSwitchOut()))
                    {
                        // Do not splice block coming from single exit switch as this prevents possible second stage recovery
                        data.spliceBlockBasic(bb);
                        count += 1;
                        // This will remove one block, so reset i
                        i = -1;
                    }
                    continue;
                }
                for (j = 1; j < bb->sizeOut(); ++j) // Are all exits to the same block? (bl)
                    if (bb->getOut(j) != bl) break;
                if (j != bb->sizeOut()) continue;

                //    ostringstream s;
                //    s << "Removing redundant branch out of block ";
                //    s << "code_" << bb->start.Target().getShortcut();
                //    bb->start.Target().printRaw(s);
                //    data.warningHeader(s.str());
                data.removeBranch(bb, 1);   // Remove the branch instruction
                count += 1;
            }
            return 0;           // Indicate full rule was applied
        }
    }
}
