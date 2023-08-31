using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief This class is used to mirror the BlockBasic objects in the fixed control-flow graph for a function
    /// The decompiler does control-flow structuring by making an initial copy of the control-flow graph,
    /// then iteratively collapsing nodes (in the copy) into \e structured nodes.  So an instance of this
    /// class acts as the mirror of an original basic block within the copy of the graph.  During the
    /// structuring process, an instance will start with an exact mirror of its underlying basic block's edges,
    /// but as the algorithm proceeds, edges may get replaced as neighboring basic blocks get collapsed, and
    /// eventually the instance will get collapsed itself and become a component of one of the \e structured
    /// block objects (BlockIf, BlockDoWhile, etc). The block that incorporates the BlockCopy as a component
    /// is accessible through getParent().
    internal class BlockCopy : FlowBlock
    {
        /// The block being mirrored by \b this (usually a BlockBasic)
        private FlowBlock copy;

        /// Construct given the block to copy
        public BlockCopy(FlowBlock bl)
        {
            copy = bl;
        }
        
        public override FlowBlock subBlock(int i) => copy;

        public override block_type getType() => block_type.t_copy;

        public override void printHeader(TextWriter s)
        {
            s.Write("Basic(copy) block ");
            @base.printHeader(s);
        }

        public override void printTree(TextWriter s, int level)
        {
            copy.printTree(s, level);
        }

        public override void printRaw(TextWriter s) 
        {
            copy.printRaw(s);
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockCopy(this);
        }

        public override FlowBlock getExitLeaf() => this;

        public override PcodeOp lastOp() => copy.lastOp();

        public override bool negateCondition(bool toporbottom)
        {
            bool res = copy.negateCondition(true);
            @base.negateCondition(toporbottom);
            return res;
        }

        public override FlowBlock getSplitPoint() => copy.getSplitPoint();

        public override bool isComplex() => copy.isComplex();

        public override void encodeHeader(Encoder encoder)
        {
            @base.encodeHeader(encoder);
            int altindex = copy.getIndex();
            encoder.writeSignedInteger(AttributeId.ATTRIB_ALTINDEX, altindex);
        }
    }
}
