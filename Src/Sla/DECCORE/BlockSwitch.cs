using ghidra;
using Sla.DECCORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ghidra.FlowBlock;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A structured \e switch construction
    ///
    /// This always has at least one component, the first, that executes the \e switch statement
    /// itself and has multiple outgoing edges. Each edge flows either to a formal exit block, or
    /// to another \e case component. All additional components are \b case components, which have
    /// either zero or one outgoing edge. If there is an edge, it flows either to another case
    /// component or to the formal exit block.  The BlockSwitch instance has zero or one outgoing edges.
    internal class BlockSwitch : BlockGraph
    {
        /// Jump table associated with this switch
        /// \brief A class for annotating and sorting the individual cases of the switch
        private JumpTable jump;
        
        private struct CaseOrder
        {
            /// The structured \e case block
            internal FlowBlock block;
            /// The first basic-block to execute within the \e case block
            internal FlowBlock basicblock;
            /// The \e label for this case, as an untyped constant
            internal ulong label;
            /// How deep in a fall-thru chain we are
            internal int depth;
            /// Who we immediately chain to, expressed as caseblocks index, -1 for no chaining
            internal int chain;
            /// Index coming out of switch to this case
            internal int outindex;
            /// (If non-zero) What type of unstructured \e case is this?
            internal uint gototype;
            /// Does this case flow to the \e exit block
            internal bool isexit;
            /// True if this is formal \e default case for the switch
            internal bool isdefault;

            /// Compare two cases
            /// Cases are compared by their label
            /// \param a is the first case to compare
            /// \param b is the second
            /// \return \b true if the first comes before the second
            internal static bool compare(CaseOrder a, CaseOrder b)
            {
                return (a.label != b.label) ? (a.label < b.label) : (a.depth < b.depth);
            }
        }

        /// Blocks associated with switch cases
        private /*mutable*/ List<CaseOrder> caseblocks = new List<CaseOrder>();

        /// Add a new \e case to this switch
        /// Associate a structured block as a full \e case of \b this switch.
        /// \param switchbl is the underlying switch statement block
        /// \param bl is the new block to make into a case
        /// \param gt gives the unstructured branch type if the switch edge to the new case was unstructured (zero otherwise)
        private void addCase(FlowBlock switchbl, FlowBlock bl, uint gt)
        {
            CaseOrder curcase = new CaseOrder();
            caseblocks.Add(curcase);
            FlowBlock basicbl = bl.getFrontLeaf().subBlock(0);
            curcase.block = bl;
            curcase.basicblock = basicbl;
            curcase.label = 0;
            curcase.depth = 0;
            curcase.chain = -1;
            int inindex = basicbl.getInIndex(switchbl);
            if (inindex == -1) {
                throw new LowlevelError("Case block has become detached from switch");
            }
            curcase.outindex = basicbl.getInRevIndex(inindex);
            curcase.gototype = gt;
            curcase.isexit = (gt == 0) && (bl.sizeOut() == 1);
            curcase.isdefault = switchbl.isDefaultBranch(curcase.outindex);
        }

        /// Construct given the multi-exit root block
        public BlockSwitch(FlowBlock ind)
        {
            jump = ind.getJumptable();
        }

        /// Build annotated CaseOrder objects
        /// Given the list of components for the switch structure, build the annotated descriptions
        /// of the cases.  Work out flow between cases and if there are any unstructured cases.
        /// The first FlowBlock in the component list is the switch component itself.  All other
        /// FlowBlocks in the list are the \e case components.
        /// \param switchbl is the underlying basic block, with multiple outgoing edges, for the switch
        /// \param cs is the list of switch and case components
        public void grabCaseBasic(FlowBlock switchbl, List<FlowBlock> cs)
        {
            // Map from from switchtarget's outindex to position in caseblocks
            List<int> casemap = new List<int>(switchbl.sizeOut());
            for(int index = 0; index < switchbl.sizeOut(); index++) {
                casemap.Add(-1);
            }
            caseblocks.Clear();
            for (int i = 1; i < cs.Count; ++i) {
                FlowBlock casebl = cs[i];
                addCase(switchbl, casebl, 0);
                // Build map from outindex to caseblocks index
                casemap[caseblocks[i - 1].outindex] = i - 1;
            }
            // Fillin fallthru chaining
            for (int i = 0; i < caseblocks.Count; ++i) {
                CaseOrder curcase = caseblocks[i];
                FlowBlock casebl = curcase.block;
                if (casebl.getType() == block_type.t_goto) {
                    // All fall-thru blocks are plain gotos
                    FlowBlock targetbl = ((BlockGoto)casebl).getGotoTarget();
                    FlowBlock basicbl = targetbl.getFrontLeaf().subBlock(0);
                    int inindex = basicbl.getInIndex(switchbl);
                    if (inindex == -1) {
                        // Goto target is not another switch case
                        continue;
                    }
                    curcase.chain = casemap[basicbl.getInRevIndex(inindex)];
                }
            }
            if (cs[0].getType() == block_type.t_multigoto) {
                // Check if some of the main switch edges were marked as goto
                BlockMultiGoto? gotoedgeblock = (BlockMultiGoto)cs[0];
                int numgoto = gotoedgeblock.numGotos();
                for (int i = 0; i < numgoto; ++i) {
                    addCase(switchbl, gotoedgeblock.getGoto(i), f_goto_goto);
                }
            }
        }

        ///Get the root switch component
        public FlowBlock getSwitchBlock() => getBlock(0);

        /// Get the number of cases
        public int getNumCaseBlocks() => caseblocks.Count;

        /// Get the i-th \e case FlowBlock
        public FlowBlock getCaseBlock(int i) => caseblocks[i].block;

        /// \brief Get the number of labels associated with one \e case block
        /// \param i is the index of the \e case block
        /// \return the number of labels put on the associated block
        public int getNumLabels(int i) => jump.numIndicesByBlock(caseblocks[i].basicblock);

        /// \brief Get a specific label associated with a \e case block
        /// \param i is the index of the \e case block
        /// \param j is the index of the specific label
        /// \return the label as an untyped constant
        public ulong getLabel(int i, int j)
            => jump.getLabelByIndex(jump.getIndexByBlock(caseblocks[i].basicblock, j));

        /// Is the i-th \e case the \e default case
        public bool isDefaultCase(int i) => caseblocks[i].isdefault;

        /// Get the edge type for the i-th \e case block
        public uint getGotoType(int i) => caseblocks[i].gototype;

        /// Does the i-th \e case block exit the switch?
        public bool isExit(int i) => caseblocks[i].isexit;

        /// Get the data-type of the switch variable
        /// Drill down to the variable associated with the BRANCHIND itself, and return its data-type
        /// \return the Datatype associated with the switch variable
        public Datatype getSwitchType()
        {
            PcodeOp op = jump.getIndirectOp();
            return op.getIn(0).getHighTypeReadFacing(op);
        }

        public override block_type getType() => block_type.t_switch;

        public override void markUnstructured()
        {
            base.markUnstructured();
            for (int i = 0; i < caseblocks.size(); ++i) {
                if (caseblocks[i].gototype ==  f_goto_goto)
                    markCopyBlock(caseblocks[i].block, f_unstructured_targ);
            }
        }

        public override void scopeBreak(int curexit, int curloopexit)
        {
            // New scope, current loop exit = curexit
            getBlock(0).scopeBreak(-1, curexit); // Top block has multiple exits
            for (int i = 0; i < caseblocks.size(); ++i) {
                FlowBlock bl = caseblocks[i].block;
                if (caseblocks[i].gototype != 0) {
                    if (bl.getIndex() == curexit) // A goto that goes straight to exit, print is (empty) break
                        caseblocks[i].gototype = f_break_goto;
                }
                else {
                    // All case blocks are either plaingotos (curexit doesn't matter)
                    //                            exitpoints (exit to switches exit   curexit = curexit)
                    bl.scopeBreak(curexit, curexit);
                }
            }
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Switch block ");
            base.printHeader(s);
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockSwitch(this);
        }

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            if (getBlock(0) == bl)
                return (FlowBlock)null;   // Don't know what will execute

            // Can only evaluate this if bl is a case block that falls through to another case block.
            // Otherwise there is a break statement in the flow
            if (bl.getType() != t_goto)    // Fallthru must be a goto block
                return (FlowBlock)null;
            int i;
            // Look for block to find flow after
            for (i = 0; i < caseblocks.size(); ++i)
                if (caseblocks[i].block == bl) break;
            if (i == caseblocks.size()) return (FlowBlock)null; // Didn't find block

            i = i + 1;                  // Blocks are printed in fallthru order, "flow" is to next block in this order
            if (i < caseblocks.size())
                return caseblocks[i].block.getFrontLeaf();
            // Otherwise we are at last block of switch, flow is to exit of switch
            if (getParent() == (FlowBlock)null) return (FlowBlock)null;
            return getParent().nextFlowAfter(this);
        }

        public override void finalizePrinting(Funcdata data)
        {
            // Make sure to still recurse
            // We need to order the cases based on the label
            // First populate the label and depth fields of the CaseOrder objects
            @base.finalizePrinting(data);
            for (int i = 0; i < caseblocks.Count; ++i) {
                // Construct the depth parameter, to sort fall-thru cases
                CaseOrder curcase = caseblocks[i];
                int j = curcase.chain;
                while (j != -1) {
                    // Run through the fall-thru chain
                    if (caseblocks[j].depth != 0) {
                        // Break any possible loops (already visited this node)
                        break;
                    }
                    // Mark non-roots of chains
                    caseblocks[j].depth = -1;
                    j = caseblocks[j].chain;
                }
            }
            for (int i = 0; i < caseblocks.Count; ++i) {
                CaseOrder curcase = caseblocks[i];
                if (jump.numIndicesByBlock(curcase.basicblock) > 0) {
                    if (curcase.depth == 0) {
                        // Only set label on chain roots
                        int ind = jump.getIndexByBlock(curcase.basicblock, 0);
                        curcase.label = jump.getLabelByIndex(ind);
                        int j = curcase.chain;
                        int depthcount = 1;
                        while (j != -1) {
                            if (caseblocks[j].depth > 0) {
                                // Has this node had its depth set. Break any possible loops.
                                break;
                            }
                            caseblocks[j].depth = depthcount++;
                            caseblocks[j].label = curcase.label;
                            j = caseblocks[j].chain;
                        }
                    }
                }
                else {
                    // Should never happen
                    curcase.label = 0;
                }
            }
            // Do actual sort of the cases based on label
            stable_sort(caseblocks.begin(), caseblocks.end(), CaseOrder::compare);
        }
    }
}
