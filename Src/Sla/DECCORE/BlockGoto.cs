using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A block that terminates with an unstructured (goto) branch to another block
    /// The \e goto must be an unconditional branch.  The instance keeps track of the target block and
    /// will emit the branch as some form of formal branch statement (goto, break, continue).
    /// From the point of view of control-flow structuring, this block has \e no output edges. The
    /// algorithm handles edges it can't structure by encapsulating it in the BlockGoto class and
    /// otherwise removing the edge from the structured view of the graph.
    internal class BlockGoto : BlockGraph
    {
        // The target block of the unstructured branch
        private FlowBlock gototarget;
        // The type of unstructured branch (f_goto_goto, f_break_goto, etc.)
        private block_flags gototype;

        /// Construct given target block
        public BlockGoto(FlowBlock bl)
        {
            gototarget = bl;
            gototype = block_flags.f_goto_goto;
        }

        /// Get the target block of the goto
        public FlowBlock getGotoTarget() => gototarget;

        /// Get the type of unstructured branch
        public FlowBlock.block_flags getGotoType() => gototype;

        /// Should a formal goto statement be emitted
        /// Under rare circumstances, the emitter can place the target block of the goto immediately
        /// after this goto block.  In this case, because the control-flow is essentially a fall-thru,
        /// there should not be a formal goto statement emitted.
        /// Check if the goto is to the next block in flow in which case the goto should not be printed.
        /// \return \b true if the goto should be printed formally
        public bool gotoPrints()
        {
            if (getParent() != null) {
                FlowBlock nextbl = getParent().nextFlowAfter(this);
                FlowBlock gotobl = getGotoTarget().getFrontLeaf();
                return (gotobl != nextbl);
            }
            return false;
        }

        public override block_type getType() => block_type.t_goto;

        public override void markUnstructured()
        {
            // Recurse
            base.markUnstructured();
            if (gototype == FlowBlock.block_flags.f_goto_goto) {
                if (gotoPrints()) {
                    markCopyBlock(gototarget, FlowBlock.block_flags.f_unstructured_targ);
                }
            }
        }

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // Recurse
            getBlock(0).scopeBreak(gototarget.getIndex(), curloopexit);

            // Check if our goto hits the current loop exit
            if (curloopexit == gototarget.getIndex()) {
                // If so, our goto is a break
                gototype = FlowBlock.block_flags.f_break_goto;
            }
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Plain goto block ");
            base.printHeader(s);
        }

        public override void printRaw(TextWriter s) => getBlock(0).printRaw(s);

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockGoto(this);
        }

        public override FlowBlock? getExitLeaf() => getBlock(0).getExitLeaf();

        public override PcodeOp lastOp() => getBlock(0).lastOp();

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            // Return the block containing the next statement in flow
            return getGotoTarget().getFrontLeaf();
        }

        public override void encodeBody(Encoder encoder)
        {
            base.encodeBody(encoder);
            encoder.openElement(ElementId.ELEM_TARGET);
            FlowBlock leaf = gototarget.getFrontLeaf();
            int depth = gototarget.calcDepth(leaf);
            encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, leaf.getIndex());
            encoder.writeSignedInteger(AttributeId.ATTRIB_DEPTH, depth);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_TYPE, gototype);
            encoder.closeElement(ElementId.ELEM_TARGET);
        }
    }
}
