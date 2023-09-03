using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An infinite loop structure
    ///
    /// This has exactly one component with one outgoing edge that flows into itself.
    /// The BlockInfLoop instance has zero outgoing edges.
    internal class BlockInfLoop : BlockGraph
    {
        public override block_type getType() => block_type.t_infloop;

        public override void markLabelBumpUp(bool bump)
        {
            // infloops steal lower blocks labels
            base.markLabelBumpUp(true);
            if (!bump) {
                clearFlag(block_flags.f_label_bumpup);
            }
        }

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // A new loop scope, current loop exit becomes curexit
            // Exits into itself
            getBlock(0).scopeBreak(getBlock(0).getIndex(), curexit);
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Infinite loop block ");
            base.printHeader(s);
        }

        public override void emit(PrintLanguage lng) 
        {
            lng.emitBlockInfLoop(this);
        }

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            // Will execute first block of infloop
            FlowBlock? nextbl = getBlock(0);
            if (nextbl != null) {
                nextbl = nextbl.getFrontLeaf();
            }
            return nextbl;
        }
    }
}
