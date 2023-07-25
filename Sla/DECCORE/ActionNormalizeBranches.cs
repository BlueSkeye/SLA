using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Flip conditional control-flow so that \e preferred comparison operators are used
    /// This is used as an alternative to the standard algorithm that structures control-flow, when
    /// normalization of the data-flow is important but structured source code doesn't need to be emitted.
    internal class ActionNormalizeBranches : Action
    {
        /// Constructor
        public ActionNormalizeBranches(string g)
            : base(0,"normalizebranches", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) {
                return null;
            }
            return new ActionNormalizeBranches(getGroup());
        }

        public override int apply(Funcdata data)
        {
            BlockGraph graph = data.getBasicBlocks();
            List<PcodeOp> fliplist = new List<PcodeOp>();

            for (int i = 0; i < graph.getSize(); ++i) {
                BlockBasic bb = (BlockBasic)graph.getBlock(i);
                if (bb.sizeOut() != 2) {
                    continue;
                }
                PcodeOp cbranch = bb.lastOp();
                if (cbranch == null) {
                    continue;
                }
                if (cbranch->code() != CPUI_CBRANCH) {
                    continue;
                }
                fliplist.Clear();
                if (opFlipInPlaceTest(cbranch, fliplist) != 0){
                    continue;
                }
                opFlipInPlaceExecute(data, fliplist);
                bb.flipInPlaceExecute();
                // Indicate a change was made
                count += 1;
            }
            // Clear any ops deleted by opFlipInPlaceExecute
            data.clearDeadOps();
            return 0;
        }
    }
}
