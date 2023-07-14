using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.FlowBlock;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief A basic "if" block
    /// This represents a formal "if" structure in code, with a expression for the condition, and
    /// one or two bodies of the conditionally executed code. An instance has one, two, or three components.
    /// One component is always the \e conditional block.  If there is a second component, it is
    /// the block of code executed when the condition is true.  If there is a third component, it
    /// is the "else" block, executed when the condition is false.
    ///
    /// If there is only one component, this represents the case where the conditionally executed
    /// branch is unstructured.  This is generally emitted where the conditionally executed body
    /// is the single \e goto statement.
    ///
    /// A BlockIf will always have at most one (structured) exit edge. With one component, one of the edges of
    /// the conditional component is unstructured. With two components, one of the conditional block
    /// edges flows to the body block, and the body's out edge and the remaining conditional block out
    /// edge flow to the same exit block. With three components, the one conditional edge flows to the
    /// \e true body block, the other conditional edge flows to the \e false body block, and outgoing
    /// edges from the body blocks, if they exist, flow to the same exit block.
    internal class BlockIf : BlockGraph
    {
        /// The type of unstructured edge (if present)
        private uint gototype;
        /// The target FlowBlock of the unstructured edge (if present)
        private FlowBlock? gototarget;

        /// Constructor
        public BlockIf()
        {
            gototype = f_goto_goto;
            gototarget = null;
        }

        /// Mark the target of the unstructured edge
        public void setGotoTarget(FlowBlock bl)
        {
            gototarget = bl;
        }

        /// Get the target of the unstructured edge
        public FlowBlock getGotoTarget() => gototarget;

        /// Get the type of unstructured edge
        public uint getGotoType() => gototype;

        public override block_type getType() => block_type.t_if;
        
        public override void markUnstructured()
        {
            base.markUnstructured(); // Recurse
            if ((gototarget != null) && (gototype == f_goto_goto)) {
                markCopyBlock(gototarget, f_unstructured_targ);
            }
        }
        public override void scopeBreak(int curexit, int curloopexit)
        {
            // Condition block has multiple exits
            // Blocks don't flow into one another, but share same exit block
            getBlock(0).scopeBreak(-1, curloopexit);
            for (int i = 1; i < getSize(); ++i) {
                getBlock(i).scopeBreak(curexit, curloopexit);
            }
            if ((gototarget != null) && (gototarget.getIndex() == curloopexit)) {
                gototype = f_break_goto;
            }
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("If block ");
            base.printHeader(s);
        }

        public override void emit(PrintLanguage lng) 
        {
            lng.emitBlockIf(this);
        }

        public override bool preferComplement(Funcdata data)
        {
            if (getSize() != 3) {
                // If we are an if/else
                return false;
            }

            FlowBlock? split = getBlock(0).getSplitPoint();
            if (split == null) {
                return false;
            }
            List<PcodeOp> fliplist = new List<PcodeOp>();
            if (0 != split.flipInPlaceTest(fliplist)) {
                return false;
            }
            split.flipInPlaceExecute();
            opFlipInPlaceExecute(data, fliplist);
            swapBlocks(1, 2);
            return true;
        }

        public override FlowBlock getExitLeaf()
        {
            // In the special case of an ifgoto block, we do have an exit leaf
            if (getSize() == 1) {
                return getBlock(0).getExitLeaf();
            }
            return null;
        }

        public override PcodeOp? lastOp()
        {
            // In the special case of an ifgoto block, we do have a last op, otherwise we don't
            return (getSize() == 1) ? getBlock(0).lastOp() : null;
        }

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            if (getBlock(0) == bl) {
                // Do not know where flow goes
                return null;
            }
            if (getParent() == null) {
                return null;
            }
            return getParent().nextFlowAfter(this);
        }

        public override void encodeBody(Encoder encoder)
        {
            base.encodeBody(encoder);
            if (getSize() == 1) {
                if (null == gototarget) {
                    throw new InvalidOperationException();
                }
                // If this is a if GOTO block
                FlowBlock leaf = gototarget.getFrontLeaf() ?? throw new BugException();
                int depth = gototarget.calcDepth(leaf);
                encoder.openElement(ElementId.ELEM_TARGET);
                encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, leaf.getIndex());
                encoder.writeSignedInteger(AttributeId.ATTRIB_DEPTH, depth);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_TYPE, gototype);
                encoder.closeElement(ElementId.ELEM_TARGET);
            }
        }
    }
}
