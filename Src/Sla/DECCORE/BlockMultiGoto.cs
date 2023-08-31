using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A block with multiple edges out, at least one of which is an unstructured (goto) branch.
    /// An instance of this class is used to mirror a basic block with multiple out edges at the point
    /// where one of the edges can't be structured.  The instance keeps track of this edge but otherwise
    /// presents a view to the structuring algorithm as if the edge didn't exist.  If at a later point,
    /// more edges can't be structured, the one instance can hold this information as well.
    internal class BlockMultiGoto : BlockGraph
    {
        /// List of goto targets from this block
        private List<FlowBlock> gotoedges;
        /// True if one of the unstructured edges is the formal switch \e default edge
        private bool defaultswitch;

        /// Construct given the underlying multi-exit block
        public BlockMultiGoto(FlowBlock bl)
        {
            defaultswitch = false;
        }

        /// Mark that this block holds an unstructured switch default
        public void setDefaultGoto()
        {
            defaultswitch = true;
        }

        /// Does this block hold an unstructured switch default edge
        public bool hasDefaultGoto() => defaultswitch;

        /// Mark the edge from \b this to the given FlowBlock as unstructured
        public void addEdge(FlowBlock bl)
        {
            gotoedges.Add(bl);
        }

        /// Get the number of unstructured edges
        public int numGotos() => gotoedges.Count;

        /// Get the target FlowBlock along the i-th unstructured edge
        public FlowBlock getGoto(int i)
        {
            return gotoedges[i] ;
        }
  
        public override block_type getType() => block_type.t_multigoto;

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // Recurse
            getBlock(0).scopeBreak(-1, curloopexit);
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Multi goto block ");
            base.printHeader(s);
        }
        
        public override void printRaw(TextWriter s)
        {
            getBlock(0).printRaw(s);
        }

        public override void emit(PrintLanguage lng)
        {
            getBlock(0).emit(lng);
        }

        public override FlowBlock? getExitLeaf() => getBlock(0).getExitLeaf();

        public override PcodeOp lastOp() => getBlock(0).lastOp();
        
        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            // The child of this can never be a BlockGoto
            return null;
        }

        public override void encodeBody(Encoder encoder)
        {
            base.encodeBody(encoder);
            for (int i = 0; i < gotoedges.Count; ++i) {
                FlowBlock gototarget = gotoedges[i];
                FlowBlock leaf = gototarget.getFrontLeaf() ?? throw new ApplicationException();
                int depth = gototarget.calcDepth(leaf);
                encoder.openElement(ElementId.ELEM_TARGET);
                encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, leaf.getIndex());
                encoder.writeSignedInteger(AttributeId.ATTRIB_DEPTH, depth);
                encoder.closeElement(ElementId.ELEM_TARGET);
            }
        }
    }
}
