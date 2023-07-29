using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A series of blocks that execute in sequence.
    /// When structuring control-flow, an instance of this class represents blocks
    /// that execute in sequence and fall-thru to each other. In general, the component
    /// blocks may not be basic blocks and can have their own sub-structures.
    internal class BlockList : BlockGraph
    {
        public override block_type getType() => block_type.t_ls;

        public override void printHeader(TextWriter s)
        {
            s.Write("List block ");
            @base.printHeader(s);
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockLs(this);
        }

        public override FlowBlock? getExitLeaf()
        {
            return (getSize() == 0) ? null : getBlock(getSize() - 1).getExitLeaf();
        }

        public override PcodeOp lastOp()
        {
            // Get last instruction of last block
            return (getSize() == 0) ? null : getBlock(getSize() - 1).lastOp();
        }

        public override bool negateCondition(bool toporbottom)
        {
            FlowBlock bl = getBlock(getSize() - 1);
            // Negate condition of last block
            bool res = bl.negateCondition(false);
            // Flip order of outgoing edges
            @base.negateCondition(toporbottom);
            return res;
        }

        public override FlowBlock? getSplitPoint()
        {
            return (getSize() == 0) ? null : getBlock(getSize() - 1).getSplitPoint();
        }
    }
}
