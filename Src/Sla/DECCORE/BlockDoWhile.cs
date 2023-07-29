using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A loop structure where the condition is checked at the bottom.
    /// This has exactly one component with two outgoing edges: one edge flows to itself,
    /// the other flows to the exit block. The BlockDoWhile instance has exactly one outgoing edge.
    internal class BlockDoWhile : BlockGraph
    {
        public override block_type getType() => block_type.t_dowhile;

        public override void markLabelBumpUp(bool bump)
        {
            // dowhiles steal lower blocks labels
            @base.markLabelBumpUp(true);
            if (!bump) {
                clearFlag(f_label_bumpup);
            }
        }

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // A new loop scope, current loop exit becomes curexit
            // Multiple exits
            getBlock(0).scopeBreak(-1, curexit);
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Dowhile block ");
            @base.printHeader(s);
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockDoWhile(this);
        }
        
        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            // Don't know what will execute next
            return null;
        }
    }
}
