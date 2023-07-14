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
    /// \brief A loop structure where the condition is checked at the top.
    /// This has exactly two components: one conditional block which evaluates when the
    /// loop terminates, and one body block.  The conditional block has two outgoing edges,
    /// one to the body block and one to the exit block.  The body block has one outgoing edge
    /// back to the conditional block.  The BlockWhileDo instance has exactly one outgoing edge.
    ///
    /// Overflow syntax refers to the situation where there is a proper BlockWhileDo structure but
    /// the conditional block is too long or complicated to emit as a single conditional expression.
    /// An alternate `while(true) { }` form is used instead.
    ///
    /// If an iterator op is provided, the block will be printed using \e for loop syntax,
    /// `for(i=0;i<10;++i)` where an \e initializer statement and \e iterator statement are
    /// printed alongside the \e condition statement.  Otherwise, \e while loop syntax is used
    /// `while(i<10)`
    internal class BlockWhileDo : BlockGraph
    {
        /// Statement used as \e for loop initializer
        private /*mutable*/ PcodeOp? initializeOp;
        /// Statement used as \e for loop iterator
        private /*mutable*/ PcodeOp? iterateOp;
        /// MULTIEQUAL merging loop variable
        private /*mutable*/ PcodeOp? loopDef;

        /// Find a \e loop \e variable
        /// Try to find a Varnode that represents the controlling \e loop \e variable for \b this loop.
        /// The Varnode must be:
        ///   - tested by the exit condition
        ///   - have a MULTIEQUAL in the head block
        ///   - have a modification coming in from the tail block
        ///   - the modification must be the last op or moveable to the last op
        ///
        /// If the loop variable is found, this routine sets the \e iterateOp and the \e loopDef.
        /// \param cbranch is the CBRANCH implementing the loop exit
        /// \param head is the head basic-block of the loop
        /// \param tail is the tail basic-block of the loop
        /// \param lastOp is the precomputed last PcodeOp of tail that isn't a BRANCH
        private void findLoopVariable(PcodeOp cbranch, BlockBasic head, BlockBasic tail,
            PcodeOp lastOp)
        {
            Varnode vn = cbranch.getIn(1);
            if (!vn.isWritten()) {
                // No loop variable found
                return;
            }
            PcodeOp op = vn.getDef();
            int slot = tail.getOutRevIndex(0);

            PcodeOpNode[] path = new PcodeOpNode[4];
            int count = 0;
            if (op.isCall() || op.isMarker()) {
                return;
            }
            path[0].op = op;
            path[0].slot = 0;
            while (count >= 0) {
                PcodeOp curOp = path[count].op;
                int ind = path[count].slot++;
                if (ind >= curOp->numInput()) {
                    count -= 1;
                    continue;
                }
                Varnode nextVn = curOp.getIn(ind);
                if (!nextVn.isWritten()) {
                    continue;
                }
                PcodeOp defOp = nextVn.getDef();
                if (defOp.code() == CPUI_MULTIEQUAL) {
                    if (defOp.getParent() != head) {
                        continue;
                    }
                    Varnode itvn = defOp.getIn(slot);
                    if (!itvn.isWritten()) {
                        continue;
                    }
                    PcodeOp possibleIterate = itvn.getDef();
                    if (possibleIterate.getParent() == tail) {
                        // Found proper head/tail configuration
                        if (possibleIterate.isMarker()) {
                            // No iteration in tail
                            continue;
                        }
                        if (!possibleIterate.isMoveable(lastOp)) {
                            // Not the final statement
                            continue;
                        }
                        loopDef = defOp;
                        iterateOp = possibleIterate;
                        // Found the loop variable
                        return;
                    }
                }
                else {
                    if (count == 3) {
                        continue;
                    }
                    if (defOp.isCall() || defOp.isMarker()) {
                        continue;
                    }
                    count += 1;
                    path[count].op = defOp;
                    path[count].slot = 0;
                }
            }
            // No loop variable found
            return;
        }

        /// Find the for-loop initializer op
        /// Given a control flow loop, try to find a putative initializer PcodeOp for the loop variable.
        /// The initializer must be read by read by \e loopDef and by in a block that
        /// flows only into the loop.  If an initializer is found, then
        /// \e initializeOp is set and the lastOp (not including a branch) in the initializer
        /// block is returned. Otherwise null is returned.
        /// \param head is the head block of the loop
        /// \param slot is the block input coming from the loop tail
        /// \return the last PcodeOp in the initializer's block
        private PcodeOp? findInitializer(BlockBasic head, int slot)
        {
            if (head.sizeIn() != 2) {
                return null;
            }
            slot = 1 - slot;
            Varnode initVn = loopDef.getIn(slot);
            if (!initVn.isWritten()) {
                return null;
            }
            PcodeOp res = initVn.getDef();
            if (res.isMarker()) {
                return null;
            }
            FlowBlock initialBlock = res.getParent();
            if (initialBlock != head.getIn(slot)) {
                // Statement must terminate in block flowing to head
                return null;
            }
            PcodeOp lastOp = initialBlock.lastOp();
            if (lastOp == null) {
                return null;
            }
            if (initialBlock.sizeOut() != 1) {
                // Initializer block must flow only to for loop
                return null;
            }
            if (lastOp.isBranch()) {
                lastOp = lastOp.previousOp();
                if (lastOp == null) {
                    return null;
                }
            }
            initializeOp = res;
            return lastOp;
        }

        /// Test that given statement is terminal and explicit
        /// For-loop initializer or iterator statements must be the final statement in
        /// their respective basic block. This method tests that iterateOp/initializeOp (specified
        /// by \e slot) is the root of or can be turned into the root of a terminal statement.
        /// The root output must be an explicit variable being read by the
        /// \e loopDef MULTIEQUAL at the top of the loop. If the root is not the last
        /// PcodeOp in the block, an attempt is made to move it.
        /// Return the root PcodeOp if all these conditions are met, otherwise return null.
        /// \param data is the function containing the while loop
        /// \param slot is the slot read by \e loopDef from the output of the statement
        /// \return an explicit statement or null
        private PcodeOp? testTerminal(Funcdata data, int slot)
        {
            Varnode vn = loopDef.getIn(slot);
            if (!vn.isWritten()) {
                return null;
            }
            PcodeOp finalOp = vn.getDef();
            BlockBasic parentBlock = (BlockBasic)loopDef.getParent().getIn(slot);
            PcodeOp resOp = finalOp;
            if (finalOp.code() == CPUI_COPY && finalOp.notPrinted()) {
                vn = finalOp.getIn(0);
                if (!vn.isWritten()) {
                    return null;
                }
                resOp = vn.getDef();
                if (resOp.getParent() != parentBlock) {
                    return null;
                }
            }

            if (!vn.isExplicit()) {
                return null;
            }
            if (resOp.notPrinted()) {
                // Statement MUST be printed
                return null;
            }

            // finalOp MUST be the last op in the basic block (except for the branch)
            PcodeOp lastOp = finalOp.getParent().lastOp();
            if (lastOp.isBranch()) {
                lastOp = lastOp.previousOp();
            }
            if (!data.moveRespectingCover(finalOp, lastOp)) {
                return null;
            }
            return resOp;
        }

        /// Return \b false if the iterate statement is of an unacceptable form
        /// Make sure the loop variable is involved as input in the iterator statement.
        /// \return \b true if the loop variable is an input to the iterator statement
        private bool testIterateForm()
        {
            Varnode targetVn = loopDef.getOut();
            HighVariable high = targetVn.getHigh();

            List<PcodeOpNode> path = new List<PcodeOpNode>();
            PcodeOp op = iterateOp;
            path.Add(new PcodeOpNode(op, 0));
            while (0 != path.Count) {
                PcodeOpNode node = path[path.Count - 1];
                if (node.op->numInput() <= node.slot) {
                    path.RemoveAt(path.Count - 1);
                    continue;
                }
                Varnode vn = node.op.getIn(node.slot);
                node.slot += 1;
                if (vn.isAnnotation()) {
                    continue;
                }
                if (vn.getHigh() == high) {
                    return true;
                }
                if (vn.isExplicit()) {
                    // Truncate at explicit
                    continue;
                }
                if (!vn.isWritten()) {
                    continue;
                }
                op = vn.getDef();
                path.Add(new PcodeOpNode(vn.getDef(), 0));
            }
            return false;
        }

        /// Constructor
        public BlockWhileDo()
        {
            initializeOp = null;
            iterateOp = null;
            loopDef = null;
        }

        /// Get root of initialize statement or null
        public PcodeOp? getInitializeOp() => initializeOp;

        /// Get root of iterate statement or null
        public PcodeOp? getIterateOp() => iterateOp;

        /// Does \b this require overflow syntax
        public bool hasOverflowSyntax()
        {
            return ((getFlags() &f_whiledo_overflow)!= 0);
        }

        /// Set that \b this requires overflow syntax
        public void setOverflowSyntax()
        {
            setFlag(f_whiledo_overflow);
        }

        public override block_type getType() => t_whiledo;

        public override void markLabelBumpUp(bool bump)
        {
            // whiledos steal lower blocks labels
            base.markLabelBumpUp(true);
            if (!bump) {
                clearFlag(f_label_bumpup);
            }
        }

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // A new loop scope (current loop exit becomes curexit)
            // Top block has multiple exits
            getBlock(0).scopeBreak(-1, curexit);
            // Exits into topblock
            getBlock(1).scopeBreak(getBlock(0).getIndex(), curexit);
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Whiledo block ");
            if (hasOverflowSyntax()) {
                s.Write("(overflow) ");
            }
            base.printHeader(s);
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockWhileDo(this);
        }

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            if (getBlock(0) == bl) {
                // Don't know what will execute next
                return null;
            }

            // Will execute first block of while
            FlowBlock nextbl = getBlock(0);
            if (nextbl != null) {
                nextbl = nextbl.getFrontLeaf();
            }
            return nextbl;
        }

        /// Determine if \b this block can be printed as a \e for loop, with an \e initializer statement
        /// extracted from the previous block, and an \e iterator statement extracted from the body.
        /// \param data is the function containing \b this loop
        public override void finalTransform(Funcdata data)
        {
            base.finalTransform(data);
            if (!data.getArch().analyze_for_loops) {
                return;
            }
            if (hasOverflowSyntax()) {
                return;
            }
            FlowBlock? copyBl = getFrontLeaf();
            if (copyBl == null) {
                return;
            }
            BlockBasic head = (BlockBasic)copyBl.subBlock(0);
            if (head.getType() != t_basic) {
                return;
            }
            // There must be a last op in body, for there to be an iterator statement
            PcodeOp lastOp = getBlock(1).lastOp();
            if (lastOp == null) {
                return;
            }
            BlockBasic tail = lastOp.getParent();
            if (tail.sizeOut() != 1) {
                return;
            }
            if (tail.getOut(0) != head) {
                return;
            }
            PcodeOp? cbranch = getBlock(0).lastOp();
            if (cbranch == null || cbranch.code() != CPUI_CBRANCH) {
                return;
            }
            if (lastOp.isBranch()) {
                // Convert lastOp to -point- iterateOp must appear after
                lastOp = lastOp.previousOp();
                if (lastOp == null) {
                    return;
                }
            }

            findLoopVariable(cbranch, head, tail, lastOp);
            if (iterateOp == null) {
                return;
            }

            if (iterateOp != lastOp) {
                data.opUninsert(iterateOp);
                data.opInsertAfter(iterateOp, lastOp);
            }

            // Try to set up initializer statement
            lastOp = findInitializer(head, tail.getOutRevIndex(0));
            if (lastOp == null) {
                return;
            }
            if (!initializeOp.isMoveable(lastOp)) {
                // Turn it off
                initializeOp = null;
                return;
            }
            if (initializeOp != lastOp) {
                data.opUninsert(initializeOp);
                data.opInsertAfter(initializeOp, lastOp);
            }
        }

        /// Assume that finalTransform() has run and that all HighVariable merging has occurred.
        /// Do any final tests checking that the initialization and iteration statements are good.
        /// Extract initialization and iteration statements from their basic blocks.
        /// \param data is the function containing the loop
        public override void finalizePrinting(Funcdata data)
        {
            // Continue recursing
            base.finalizePrinting(data);
            if (iterateOp == null) {
                // For-loop printing not enabled
                return;
            }
            // TODO: We can check that iterate statement is not too complex
            int slot = iterateOp.getParent().getOutRevIndex(0);
            // Make sure iterator statement is explicit
            iterateOp = testTerminal(data, slot);
            if (iterateOp == null) {
                return;
            }
            if (!testIterateForm()) {
                iterateOp = null;
                return;
            }
            if (initializeOp == null) {
                // Last chance initializer
                findInitializer(loopDef.getParent(), slot);
            }
            if (initializeOp != null) {
                // Make sure initializer statement is explicit
                initializeOp = testTerminal(data, 1 - slot);
            }

            data.opMarkNonPrinting(iterateOp);
            if (initializeOp != null) {
                data.opMarkNonPrinting(initializeOp);
            }
        }
    }
}
