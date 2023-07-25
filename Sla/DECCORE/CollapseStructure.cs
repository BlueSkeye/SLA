using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Build a code structure from a control-flow graph (BlockGraph).
    ///
    /// This class manages the main control-flow structuring algorithm for the decompiler.
    /// In short:
    ///    - Start with a control-flow graph of basic blocks.
    ///    - Repeatedly apply:
    ///       - Search for sub-graphs matching specific code structure elements.
    ///       - Note the structure element and collapse the component nodes to a single node.
    ///    - If the process gets stuck, remove appropriate edges, marking them as unstructured.
    internal class CollapseStructure
    {
        /// Have we a made search for unstructured edges in the final DAG
        private bool finaltrace;
        /// Have we generated a \e likely \e goto list for the current innermost loop
        private bool likelylistfull;
        /// The current \e likely \e goto list
        private List<FloatingEdge> likelygoto;
        /// Iterator to the next most \e likely \e goto edge
        private list<FloatingEdge>::iterator likelyiter;
        /// The list of loop bodies for this control-flow graph
        private List<LoopBody> loopbody;
        /// Current (innermost) loop being structured
        private IEnumerator<LoopBody> loopbodyiter;
        /// The control-flow graph
        private BlockGraph graph;
        /// Number of data-flow changes made during structuring
        private int dataflow_changecount;

        /// \brief Check for switch edges that go straight to the exit block
        ///
        /// Some switch forms have edges that effectively skip the body of the switch and go straight to the exit
        /// Many jumptables schemes have a \e default (i.e. if nothing else matches) edge.  This edge cannot be a normal
        /// \b case because there would be too many labels to explicitly specify.  The edge must either be labeled as
        /// \e default or it must go straight to the exit block.  If there is a \e default edge, if it does not go
        /// straight to the exit, there can be no other edge that skips straight to the exit.
        ///
        /// If such skip edges exist, they are converted to gotos and \b false is returned.
        /// \param switchbl is the entry FlowBlock for the switch
        /// \param exitblock is the designated exit FlowBlock for the switch
        /// \return true if there are no \e skip edges
        private bool checkSwitchSkips(FlowBlock switchbl, FlowBlock exitblock)
        {
            if (exitblock == null) {
                return true;
            }

            // Is there a "default" edge that goes straight to the exitblock
            int sizeout;
            int edgenum;
            sizeout = switchbl.sizeOut();
            bool defaultnottoexit = false;
            bool anyskiptoexit = false;
            for (edgenum = 0; edgenum < sizeout; ++edgenum) {
                if (switchbl.getOut(edgenum) == exitblock) {
                    if (!switchbl.isDefaultBranch(edgenum)) {
                        anyskiptoexit = true;
                    }
                }
                else {
                    if (switchbl.isDefaultBranch(edgenum)) {
                        defaultnottoexit = true;
                    }
                }
            }

            if (!anyskiptoexit) {
                return true;
            }

            if ((!defaultnottoexit) && (switchbl.getType() == FlowBlock.block_type.t_multigoto)) {
                BlockMultiGoto multibl = (BlockMultiGoto)switchbl;
                if (multibl.hasDefaultGoto()) {
                    defaultnottoexit = true;
                }
            }
            if (!defaultnottoexit) {
                return true;
            }

            for (edgenum = 0; edgenum < sizeout; ++edgenum) {
                if (switchbl.getOut(edgenum) == exitblock) {
                    if (!switchbl.isDefaultBranch(edgenum)) {
                        switchbl.setGotoBranch(edgenum);
                    }
                }
            }
            return false;
        }

        /// \brief Mark FlowBlocks \b only reachable from a given root
        /// For a given root FlowBlock, find all the FlowBlocks that can only be reached from it,
        /// mark them and put them in a list/
        /// \param root is the given FlowBlock root
        /// \param body is the container to hold the list of reachable nodes
        private void onlyReachableFromRoot(FlowBlock root, List<FlowBlock> body)
        {
            List<FlowBlock> trial = new List<FlowBlock>();
            int i = 0;
            root.setMark();
            body.Add(root);
            while (i < body.Count()) {
                FlowBlock bl = body[i++];
                int sizeout = bl.sizeOut();
                for (int j = 0; j < sizeout; ++j) {
                    FlowBlock curbl = bl.getOut(j);
                    if (curbl.isMark()) {
                        continue;
                    }
                    int count = curbl.getVisitCount();
                    if (count == 0) {
                        // New possible extension
                        trial.Add(curbl);
                    }
                    count += 1;
                    curbl.setVisitCount(count);
                    if (count == curbl.sizeIn()) {
                        curbl.setMark();
                        body.Add(curbl);
                    }
                }
            }
            for (i = 0; i < trial.Count(); ++i) {
                // Make sure to clear the count
                trial[i].setVisitCount(0);
            }
        }

        /// Mark edges exiting the body as \e unstructured gotos
        /// The FlowBlock objects in the \b body must all be marked.
        /// \param body is the list of FlowBlock objects in the body
        /// \return the number of edges that were marked as \e unstructured
        private int markExitsAsGotos(List<FlowBlock> body)
        {
            int changecount = 0;
            for (int i = 0; i < body.Count(); ++i) {
                FlowBlock bl = body[i];
                int sizeout = bl.sizeOut();
                for (int j = 0; j < sizeout; ++j) {
                    FlowBlock curbl = bl.getOut(j);
                    if (!curbl.isMark()) {
                        // mark edge as goto
                        bl.setGotoBranch(j);
                        changecount += 1;
                    }
                }
            }
            return changecount;
        }

        /// Mark edges between root components as \e unstructured gotos
        /// Find distinct control-flow FlowBlock roots (having no incoming edges).
        /// These delineate disjoint subsets of the control-flow graph, where a subset
        /// is defined as the FlowBlock nodes that are only reachable from the root.
        /// This method searches for one disjoint subset with \b cross-over edges,
        /// edges from that subset into another.  The exiting edges for this subset are marked
        /// as \e unstructured \e gotos and \b true is returned.
        /// \return true if any cross-over edges were found (and marked)
        private bool clipExtraRoots()
        {
            for (int i = 1; i < graph.getSize(); ++i) {
                // Skip the canonical root
                FlowBlock bl = graph.getBlock(i);
                if (bl.sizeIn() != 0) {
                    continue;
                }
                List<FlowBlock> body = new List<FlowBlock>();
                onlyReachableFromRoot(bl, body);
                int count = markExitsAsGotos(body);
                LoopBody.clearMarks(body);
                if (count != 0) {
                    return true;
                }
            }
            return false;
        }

        /// Identify all the loops in this graph
        /// Identify all the distinct loops in the graph (via their back-edge) and create a LoopBody record.
        /// \param looporder is the container that will hold the LoopBody record for each loop
        private void labelLoops(List<LoopBody> looporder)
        {
            for (int i = 0; i < graph.getSize(); ++i) {
                FlowBlock bl = graph.getBlock(i);
                int sizein = bl.sizeIn();
                for (int j = 0; j < sizein; ++j) {
                    if (bl.isBackEdgeIn(j)) {
                        // back-edge coming in must be from the bottom of a loop
                        FlowBlock loopbottom = bl.getIn(j);
                        LoopBody curbody = new LoopBody(bl);
                        loopbody.Add(curbody);
                        curbody.addTail(loopbottom);
                    }
                }
            }
            looporder.Sort(LoopBody.compare_ends);
        }

        /// Identify and label all loop structure for this graph
        /// Find the loop bodies, then:
        ///   - Label all edges which exit their loops.
        ///   - Generate a partial order on the loop bodies.
        private void orderLoopBodies()
        {
            List<LoopBody> looporder = new List<LoopBody>();
            labelLoops(looporder);
            if (0 != loopbody.Count()) {
                int oldsize = looporder.Count();
                LoopBody.mergeIdenticalHeads(looporder);
                IEnumerator<LoopBody> iter;
                if (oldsize != looporder.Count()) {
                    // If there was merging
                    iter = loopbody.GetEnumerator();
                    List<LoopBody> deletedItems = new List<LoopBody>();
                    while (iter.MoveNext()) {
                        if (iter.getHead() == null) {
                            // Delete the subsumed loopbody
                            deletedItems.Add(iter.Current);
                        }
                    }
                    foreach(LoopBody deletedItem in deletedItems) {
                        loopbody.Remove(deletedItem);
                    }
                }
                foreach (LoopBody scannedBody in loopbody) {
                    List<FlowBlock> body = new List<FlowBlock>();
                    scannedBody.findBase(body);
                    scannedBody.labelContainments(body, looporder);
                    LoopBody.clearMarks(body);
                }
                // Sort based on nesting depth (deepest come first) (sorting is stable)
                loopbody.Sort();
                foreach (LoopBody scannedBody in loopbody) {
                    List<FlowBlock> body = new List<FlowBlock>();
                    scannedBody.findBase(body);
                    scannedBody.findExit(body);
                    scannedBody.orderTails();
                    scannedBody.extend(body);
                    scannedBody.labelExitEdges(body);
                    LoopBody.clearMarks(body);
                }
            }
            likelylistfull = false;
            loopbodyiter = loopbody.GetEnumerator();
        }

        /// Find likely \e unstructured edges within the innermost loop body
        /// Find the current innermost loop, make sure its \e likely \e goto edges are calculated.
        /// If there are no loops, make sure the \e likely \e goto edges are calculated for the final DAG.
        /// \return true if there are likely \e unstructured edges left to provide
        private bool updateLoopBody()
        {
            FlowBlock? loopbottom = null;
            FlowBlock? looptop = null;
            if (finaltrace) {
                // If we've already performed the final trace
                return (likelyiter == likelygoto.end());
            }
            while (loopbodyiter != loopbody.end()) {
                // Last innermost loop
                loopbottom = loopbodyiter.Current.getCurrentBounds(looptop, graph);
                if (loopbottom != null) {
                    if ((!likelylistfull) || (likelyiter != likelygoto.end())) {
                        // Reaching here means, we removed edges but loop still didn't collapse
                        // Loop still exists
                        break;
                    }
                }
                ++loopbodyiter;
                // Need to generate likely list for new loopbody (or no loopbody)
                likelylistfull = false;
                loopbottom = null;
            }
            if (likelylistfull) {
                return true;
            }
            // If we reach here, need to generate likely gotos for a new inner loop
            // Clear out any old likely gotos from last inner loop
            likelygoto.Clear();
            TraceDAG tracer = new TraceDAG(likelygoto);
            if (loopbottom != null) {
                // Trace from the top of the loop
                tracer.addRoot(looptop);
                tracer.setFinishBlock(loopbottom);
                // Set the bounds of the TraceDAG
                loopbodyiter.Current.setExitMarks(graph);
            }
            else {
                finaltrace = true;
                for (int i = 0; i < graph.getSize(); ++i) {
                    FlowBlock bl = graph.getBlock(i);
                    if (bl.sizeIn() == 0) {
                        tracer.addRoot(bl);
                    }
                }
            }
            tracer.initialize();
            tracer.pushBranches();
            if (loopbottom != null) {
                loopbodyiter.Current.emitLikelyEdges(likelygoto, graph);
                loopbodyiter.Current.clearExitMarks(graph);
            }
            likelylistfull = true;
            likelyiter = likelygoto.GetEnumerator();
            return true;
        }

        /// Select an edge to mark as  \e unstructured
        /// Pick an edge from among the \e likely \e goto list generated by a
        /// trace of the current innermost loop.  Given ongoing collapsing, this
        /// may involve updating which loop is currently innermost and throwing
        /// out potential edges whose endpoints have already been collapsed.
        /// \return the FlowBlock whose outgoing edge was marked \e unstructured or NULL
        private FlowBlock? selectGoto()
        {
            while (updateLoopBody()) {
                while (likelyiter != likelygoto.end()) {
                    int outedge;
                    FlowBlock startbl = likelyiter.Current.getCurrentEdge(outedge, graph);
                    ++likelyiter;
                    if (startbl != null) {
                        // Mark the selected branch as goto
                        startbl.setGotoBranch(outedge);
                        return startbl;
                    }
                }
            }
            if (!clipExtraRoots()) {
                throw new LowlevelError("Could not finish collapsing block structure");
            }
            return null;
        }

        /// Attempt to apply the BlockGoto structure
        /// For the given FlowBlock, look for an outgoing edge marked as \e unstructured.
        /// Create or update the BlockGoto or BlockMultiGoto structure.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockGoto(FlowBlock bl)
        {
            int sizeout = bl.sizeOut();
            for (int i = 0; i < sizeout; ++i) {
                if (bl.isGotoOut(i)) {
                    if (bl.isSwitchOut()) {
                        graph.newBlockMultiGoto(bl, i);
                        return true;
                    }
                    if (sizeout == 2) {
                        if (!bl.isGotoOut(1)) {
                            // True branch must be goto
                            if (bl.negateCondition(true)) {
                                dataflow_changecount += 1;
                            }
                        }
                        graph.newBlockIfGoto(bl);
                        return true;
                    }
                    if (sizeout == 1) {
                        graph.newBlockGoto(bl);
                        return true;
                    }
                }
            }
            return false;
        }

        /// Attempt to apply a BlockList structure
        /// Try to concatenate a straight sequences of blocks starting with the given FlowBlock.
        /// All of the internal edges should be DAG  (no \e exit, \e goto,or \e loopback).
        /// The final edge can be an exit or loopback
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockCat(FlowBlock bl)
        {
            FlowBlock outblock;
            FlowBlock outbl2;

            if (bl.sizeOut() != 1) {
                return false;
            }
            if (bl.isSwitchOut()) {
                return false;
            }
            // Must be start of chain
            if ((bl.sizeIn() == 1) && (bl.getIn(0).sizeOut() == 1)) {
                return false;
            }
            outblock = bl.getOut(0);
            // No looping
            if (outblock == bl) {
                return false;
            }
            // Nothing else can hit outblock
            if (outblock.sizeIn() != 1) {
                return false;
            }
            // Not a goto or a loopbottom
            if (!bl.isDecisionOut(0)) {
                return false;
            }
            // Switch must be resolved first
            if (outblock.isSwitchOut()) {
                return false;
            } 

            List<FlowBlock> nodes = new List<FlowBlock>();
            // The first two blocks being concatenated
            nodes.Add(bl);
            nodes.Add(outblock);

            while (outblock.sizeOut() == 1) {
                outbl2 = outblock.getOut(0);
                // No looping
                if (outbl2 == bl) {
                    break;
                }
                if (outbl2.sizeIn() != 1) {
                    // Nothing else can hit outblock
                    break;
                }
                if (!outblock.isDecisionOut(0)) {
                    // Don't use loop bottom
                    break;
                }
                if (outbl2.isSwitchOut()) {
                    // Switch must be resolved first
                    break;
                }
                outblock = outbl2;
                // Extend the cat chain
                nodes.Add(outblock);
            }
            // Concatenate the nodes into a single block
            graph.newBlockList(nodes);
            return true;
        }

        /// Attempt to apply a BlockCondition structure
        /// Try to find an OR conditions (finding ANDs by duality) starting with the given FlowBlock.
        /// The top of the OR should not perform \e gotos, the edge to the \b orblock should not
        /// be \e exit or \e loopback
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockOr(FlowBlock bl)
        {
            FlowBlock orblock;
            FlowBlock clauseblock;
            int i;
            int j;

            if (bl.sizeOut() != 2) {
                return false;
            }
            if (bl.isGotoOut(0)) {
                return false;
            }
            if (bl.isGotoOut(1)) {
                return false;
            }
            if (bl.isSwitchOut()) {
                return false;
            }
            // NOTE: complex behavior can happen in the first block because we (may) only
            // print the branch
            //  if (bl->isComplex()) return false; // Control flow too complicated for condition
            for (i = 0; i < 2; ++i) {
                orblock = bl.getOut(i);    // False out is other part of OR
                if (orblock == bl) {
                    // orblock cannot be same block
                    continue;
                }
                if (orblock.sizeIn() != 1) {
                    // Nothing else can hit orblock
                    continue;
                }
                if (orblock.sizeOut() != 2) {
                    // orblock must also be binary condition
                    continue;
                }
                if (orblock.isInteriorGotoTarget()) {
                    // No unstructured jumps into or
                    continue;
                }
                if (orblock.isSwitchOut()) {
                    continue;
                }
                if (bl.isBackEdgeOut(i)) {
                    // Don't use loop branch to get to orblock
                    continue;
                }
                if (orblock.isComplex()) {
                    continue;
                }
                // This line was always commented out.  I assume minor
                // block order variations were screwing up this rule
                clauseblock = bl.getOut(1 - i);
                if (clauseblock == bl) {
                    // No looping
                    continue;
                }
                if (clauseblock == orblock) {
                    continue;
                }
                for (j = 0; j < 2; ++j) {
                    if (clauseblock != orblock.getOut(j)) {
                        // Clauses don't match
                        continue;
                    }
                    break;
                }
                if (j == 2) {
                    continue;
                }
                if (orblock.getOut(1 - j) == bl) {
                    // No looping
                    continue;
                }
                // Do we need to check that
                //   bl->isBackEdgeOut(i)  =>  orblock->isBackEdgeOut(j)
                //   bl->isLoopExitOut(i)  =>  orblock->isLoopExitOut(j)
                if (i == 1) {
                    // orblock needs to be false out of bl
                    if (bl.negateCondition(true)) {
                        dataflow_changecount += 1;
                    }
                }
                if (j == 0) {
                    // clauseblock needs to be true out of orblock
                    if (orblock.negateCondition(true)) {
                        dataflow_changecount += 1;
                    }
                }
                graph.newBlockCondition(bl, orblock);
                return true;
            }
            return false;
        }

        /// Attempt to apply a 2 component form of BlockIf
        /// Try to structure a \e proper if structure (with no \b else clause) starting from the given FlowBlock.
        /// The edge to the clause should not be an \e exit or \e loopbottom.
        /// The outgoing edges can be \e exit or \e loopbottom.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockProperIf(FlowBlock bl)
        {
            FlowBlock clauseblock;
            FlowBlock outblock;
            int i;

            if (bl.sizeOut() != 2) {
                return false;
            }
             // Must be binary condition
            if (bl.isSwitchOut()) {
                return false;
            }

            if (bl.getOut(0) == bl) {
                // No loops
                return false;
            }
            if (bl.getOut(1) == bl) {
                return false;
            }

            if (bl.isGotoOut(0)) {
                // Neither branch must be unstructured
                return false;
            }
            if (bl.isGotoOut(1)) {
                return false;
            }

            for (i = 0; i < 2; ++i) {
                clauseblock = bl.getOut(i);
                if (clauseblock.sizeIn() != 1) {
                    continue;
                }
                 // Nothing else can hit clauseblock
                if (clauseblock.sizeOut() != 1) {
                    // Only one way out of clause
                    continue;
                }
                if (clauseblock.isSwitchOut()) {
                    // Don't use switch (possibly with goto edges)
                    continue;
                }
                if (!bl.isDecisionOut(i)) {
                    // Don't use loopbottom or exit
                    continue;
                }
                if (clauseblock.isGotoOut(0)) {
                    // No unstructured jumps out of clause
                    continue;
                }
                outblock = clauseblock.getOut(0);
                if (outblock != bl.getOut(1 - i)) {
                    // Path after clause must be the same
                    continue;
                }
                if (i == 0) {
                    // Clause must be true
                    if (bl.negateCondition(true)) {
                        dataflow_changecount += 1;
                    }
                }
                graph.newBlockIf(bl, clauseblock);
                return true;
            }
            return false;
        }

        /// Attempt to apply a 3 component form of BlockIf
        /// Try to find an if/else structure starting with the given FlowBlock.
        /// Edges into the clauses cannot be \e goto, \e exit,or \e loopback.
        /// The returning edges can be \e exit or \e loopback.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockIfElse(FlowBlock bl)
        {
            FlowBlock tc;
            FlowBlock fc;
            FlowBlock outblock;

            if (bl.sizeOut() != 2) {
                return false;
            }
             // Must be binary condition
            if (bl.isSwitchOut()) {
                return false;
            }
            if (!bl.isDecisionOut(0)) {
                return false;
            }
            if (!bl.isDecisionOut(1)) {
                return false;
            }
            tc = bl.getTrueOut();
            fc = bl.getFalseOut();
            if (tc.sizeIn() != 1) {
                // Nothing else must hit true clause
                return false;
            }
            if (fc.sizeIn() != 1) {
                // Nothing else must hit false clause
                return false;
            }
            if (tc.sizeOut() != 1) {
                // Only one exit from clause
                return false;
            }
            if (fc.sizeOut() != 1) {
                // Only one exit from clause
                return false;
            }
            outblock = tc.getOut(0);
            if (outblock == bl) {
                // No loops
                return false;
            }
            if (outblock != fc.getOut(0)) {
                // Clauses must exit to same place
                return false;
            }
            if (tc.isSwitchOut()) {
                return false;
            }
            if (fc.isSwitchOut()) {
                return false;
            }
            if (tc.isGotoOut(0)) {
                return false;
            }
            if (fc.isGotoOut(0)) {
                return false;
            }
            graph.newBlockIfElse(bl, tc, fc);
            return true;
        }

        /// Attempt to apply BlockIf where the body does not exit
        /// Try to find an if structure, where the condition clause does not exit,
        /// starting with the given FlowBlock.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockIfNoExit(FlowBlock bl)
        {
            FlowBlock clauseblock;
            int i;

            if (bl.sizeOut() != 2) {
                return false;
            }
             // Must be binary condition
            if (bl.isSwitchOut()) {
                return false;
            }
            if (bl.getOut(0) == bl) {
                return false;
            }
// No loops
            if (bl.getOut(1) == bl) {
                return false;
            }
            if (bl.isGotoOut(0)) {
                return false;
            }
            if (bl.isGotoOut(1)) {
                return false;
            }
            for (i = 0; i < 2; ++i) {
                clauseblock = bl.getOut(i);
                if (clauseblock.sizeIn() != 1) {
                    continue;
                }
                 // Nothing else must hit clause
                if (clauseblock.sizeOut() != 0) {
                    // Must be no way out of clause
                    continue;
                }
                if (clauseblock.isSwitchOut()) {
                    continue;
                }
                if (!bl.isDecisionOut(i)) {
                    continue;
                }
                //    if (clauseblock->isInteriorGotoTarget()) {
                //      bl->setGotoBranch(i);
                //      return true;
                //    }

                if (i == 0) {
                    // clause must be true out of bl
                    if (bl.negateCondition(true)) {
                        dataflow_changecount += 1;
                    }
                }
                graph.newBlockIf(bl, clauseblock);
                return true;
            }
            return false;
        }

        /// Attempt to apply the BlockWhileDo structure
        /// Try to find a while/do structure, starting with a given FlowBlock.
        /// Any \e break or \e continue must have already been collapsed as some form of \e goto.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockWhileDo(FlowBlock bl)
        {
            FlowBlock clauseblock;
            int i;

            if (bl.sizeOut() != 2) {
                return false;
            }
             // Must be binary condition
            if (bl.isSwitchOut()) {
                return false;
            }
            if (bl.getOut(0) == bl) {
                // No loops at this point
                return false;
            }
            if (bl.getOut(1) == bl) {
                return false;
            }
            if (bl.isInteriorGotoTarget()) {
                return false;
            }
            if (bl.isGotoOut(0)) {
                return false;
            }
            if (bl.isGotoOut(1)) {
                return false;
            }
            for (i = 0; i < 2; ++i) {
                clauseblock = bl.getOut(i);
                if (clauseblock.sizeIn() != 1) {
                    continue;
                }
                 // Nothing else must hit clause
                if (clauseblock.sizeOut() != 1) {
                    // Only one way out of clause
                    continue;
                }
                if (clauseblock.isSwitchOut()) {
                    continue;
                }
                if (clauseblock.getOut(0) != bl) {
                    // Clause must loop back to bl
                    continue;
                }

                // Check if we need to use overflow syntax
                bool overflow = bl.isComplex();
                if ((i == 0) != overflow) {
                    // clause must be true out of bl unless we use overflow syntax
                    if (bl.negateCondition(true)) {
                        dataflow_changecount += 1;
                    }
                }
                BlockWhileDo newbl = graph.newBlockWhileDo(bl, clauseblock);
                if (overflow) {
                    newbl.setOverflowSyntax();
                }
                return true;
            }
            return false;
        }

        /// Attempt to apply the BlockDoWhile structure
        /// Try to find a do/while structure, starting with the given FlowBlock.
        /// Any \e break and \e continue must have already been collapsed as some form of \e goto.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockDoWhile(FlowBlock bl)
        {
            int i;

            if (bl.sizeOut() != 2) {
                return false;
            }
             // Must be binary condition
            if (bl.isSwitchOut()) {
                return false;
            }
            if (bl.isGotoOut(0)) {
                return false;
            }
            if (bl.isGotoOut(1)) {
                return false;
            }
            for (i = 0; i < 2; ++i) {
                if (bl.getOut(i) != bl) {
                    continue;
                }
                 // Must loop back on itself
                if (i == 0) {
                    // must loop on true condition
                    if (bl.negateCondition(true)) {
                        dataflow_changecount += 1;
                    }
                }
                graph.newBlockDoWhile(bl);
                return true;
            }
            return false;
        }

        /// Attempt to apply the BlockInfLoop structure
        /// Try to find a loop structure with no exits, starting at the given FlowBlock.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockInfLoop(FlowBlock bl)
        {
            if (bl.sizeOut() != 1) {
                // Must only be one way out
                return false;
            }

            // If the single out edge is from a switch (BRANCHIND) and also forms an infinite
            // loop, the ruleBlockSwitch method will not hit because the switch won't have a
            // proper exit block.  So we let this method collapse it by NOT checking for switch.
            //  if (bl->isSwitchOut()) return false;

            if (bl.isGotoOut(0)) {
                return false;
            }
            if (bl.getOut(0) != bl) {
                // Must fall into itself
                return false;
            }
            graph.newBlockInfLoop(bl);
            return true;
        }

        /// Attempt to apply the BlockSwitch structure
        /// Try to find a switch structure, starting with the given FlowBlock.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleBlockSwitch(FlowBlock bl)
        {
            if (!bl.isSwitchOut()) {
                return false;
            }
            FlowBlock? exitblock = null;
            int sizeout = bl.sizeOut();

            // Find "obvious" exitblock,  is sizeIn>1 or sizeOut>1
            for (int i = 0; i < sizeout; ++i) {
                FlowBlock curbl = bl.getOut(i);
                if (curbl == bl) {
                    // Exit back to top of switch (loop)
                    exitblock = curbl;
                    break;
                }
                if (curbl.sizeOut() > 1) {
                    exitblock = curbl;
                    break;
                }
                if (curbl.sizeIn() > 1) {
                    exitblock = curbl;
                    break;
                }
            }
            if (exitblock == null) {
                // If we reach here, every immediate block out of switch must have sizeIn==1 and sizeOut<=1
                // Any immediate block that was an "exitblock" would have no cases exiting to it (because sizeIn==1)
                // If that block had an output, that output can also viably be an output.
                // So as soon as we see an immediate block with an output, we make the output the exit
                for (int i = 0; i < sizeout; ++i) {
                    FlowBlock curbl = bl.getOut(i);
                    if (curbl.isGotoIn(0)) {
                        return false;
                    }
                     // In cannot be a goto
                    if (curbl.isSwitchOut()) {
                        // Must resolve nested switch first
                        return false;
                    }
                    if (curbl.sizeOut() == 1) {
                        if (curbl.isGotoOut(0)) {
                            // Out cannot be goto
                            return false;
                        }
                        if (exitblock != null) {
                            if (exitblock != curbl.getOut(0)) {
                                return false;
                            }
                        }
                        else {
                            exitblock = curbl.getOut(0);
                        }
                    }
                }
            }
            else {
                // From here we have a determined exitblock
                for (int i = 0; i < exitblock.sizeIn(); ++i) {
                    // No in gotos to exitblock
                    if (exitblock.isGotoIn(i)) {
                        return false;
                    }
                }
                for (int i = 0; i < exitblock.sizeOut(); ++i) {
                    // No out gotos from exitblock
                    if (exitblock.isGotoOut(i)) {
                        return false;
                    }
                }
                for (int i = 0; i < sizeout; ++i) {
                    FlowBlock curbl = bl.getOut(i);
                    if (curbl == exitblock) {
                        // The switch can go straight to the exit block
                        continue;
                    }
                    if (curbl.sizeIn() > 1) {
                        // A case can only have the switch fall into it
                        return false;
                    }
                    if (curbl.isGotoIn(0)) {
                        // In cannot be a goto
                        return false;
                    }
                    if (curbl.sizeOut() > 1) {
                        // There can be at most 1 exit from a case
                        return false;
                    }
                    if (curbl.sizeOut() == 1) {
                        if (curbl.isGotoOut(0)) {
                            // Out cannot be goto
                            return false;
                        }
                        if (curbl.getOut(0) != exitblock) {
                            // which must be to the exitblock
                            return false;
                        }
                    }
                    if (curbl.isSwitchOut()) {
                        // Nested switch must be resolved first
                        return false;
                    }
                }
            }

            if (!checkSwitchSkips(bl, exitblock)) {
                // We match, but have special condition that adds gotos
                return true;
            }

            List<FlowBlock> cases = new List<FlowBlock>();
            cases.Add(bl);
            for (int i = 0; i < sizeout; ++i) {
                FlowBlock curbl = bl.getOut(i);
                if (curbl == exitblock) {
                    // Don't include exit as a case
                    continue;
                }
                cases.Add(curbl);
            }
            graph.newBlockSwitch(cases, (exitblock != null));
            return true;
        }

        /// Attempt to one switch case falling through to another
        /// Look for a switch case that falls thru to another switch case, starting
        /// with the given switch FlowBlock.
        /// \param bl is the given FlowBlock
        /// \return \b true if the structure was applied
        private bool ruleCaseFallthru(FlowBlock bl)
        {
            if (!bl.isSwitchOut()) {
                return false;
            }
            int sizeout = bl.sizeOut();
            // Count of exits that are not fallthru
            int nonfallthru = 0;
            List<FlowBlock> fallthru = new List<FlowBlock>();

            for (int i = 0; i < sizeout; ++i) {
                FlowBlock curbl = bl.getOut(i);
                if (curbl == bl) {
                    // Cannot exit to itself
                    return false;
                }
                if ((curbl.sizeIn() > 2) || (curbl.sizeOut() > 1)) {
                    nonfallthru += 1;
                }
                else if (curbl.sizeOut() == 1) {
                    FlowBlock target = curbl.getOut(0);
                    if ((target.sizeIn() == 2) && (target.sizeOut() <= 1)) {
                        int inslot = curbl.getOutRevIndex(0);
                        if (target.getIn(1 - inslot) == bl) {
                            fallthru.Add(curbl);
                        }
                    }
                }
                if (nonfallthru > 1) {
                    // Can have at most 1 other exit block
                    return false;
                }
            }
            if (fallthru.empty()) {
                // No fall thru candidates
                return false;
            }
            // Check exit block matches the 1 nonfallthru exit

            // Mark the fallthru edges as gotos
            for (int i = 0; i < fallthru.size(); ++i) {
                FlowBlock curbl = fallthru[i];
                curbl.setGotoBranch(0);
            }

            return true;
        }

        /// The main collapsing loop
        /// Collapse everything until no additional rules apply.
        /// If handed a particular FlowBlock, try simplifying from that block first.
        /// \param targetbl is the FlowBlock to start from or NULL
        /// \return the count of \e isolated FlowBlocks (with no incoming or outgoing edges)
        private int collapseInternal(FlowBlock targetbl)
        {
            int index;
            bool change, fullchange;
            int isolated_count;
            FlowBlock bl;

            do {
                do {
                    change = false;
                    index = 0;
                    isolated_count = 0;
                    while (index < graph.getSize()) {
                        if (targetbl == null) {
                            bl = graph.getBlock(index);
                            index += 1;
                        }
                        else {
                            // Pick out targeted block
                            bl = targetbl;
                            // but force a change so we still go through all blocks
                            change = true;
                            // Only target the block once
                            targetbl = null;
                            index = graph.getSize();
                        }
                        if ((bl.sizeIn() == 0) && (bl.sizeOut() == 0)) {
                            // A completely collapsed block
                            isolated_count += 1;
                            // This does not constitute a chanage
                            continue;
                        }
                        // Try each rule on the block
                        if (ruleBlockGoto(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockCat(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockProperIf(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockIfElse(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockWhileDo(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockDoWhile(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockInfLoop(bl)) {
                            change = true;
                            continue;
                        }
                        if (ruleBlockSwitch(bl)) {
                            change = true;
                            continue;
                        }
                        //      if (ruleBlockOr(bl)) {
                        //	change = true;
                        //	continue;
                        //      }
                    }
                } while (change);
                // Applying IfNoExit rule too early can cause other (preferable) rules to miss
                // Only apply the rule if nothing else can apply
                fullchange = false;
                for (index = 0; index < graph.getSize(); ++index) {
                    bl = graph.getBlock(index);
                    if (ruleBlockIfNoExit(bl)) {
                        // If no other change is possible but still blocks left, try ifnoexit
                        fullchange = true;
                        break;
                    }
                    if (ruleCaseFallthru(bl)) {
                        // Check for fallthru cases in a switch
                        fullchange = true;
                        break;
                    }
                }
            } while (fullchange);
            return isolated_count;
        }

        /// Simplify conditionals
        /// Simplify just the conditional AND/OR constructions.
        private void collapseConditions()
        {
            bool change;
            do {
                change = false;
                for (int i = 0; i < graph.getSize(); ++i) {
                    if (ruleBlockOr(graph.getBlock(i))) {
                        change = true;
                    }
                }
            } while (change);
        }

        /// Construct given a control-flow graph
        /// The initial BlockGraph should be a copy of the permanent control-flow graph.
        /// In particular the FlowBlock nodes should be BlockCopy instances.
        /// \param g is the (copy of the) control-flow graph
        public CollapseStructure(BlockGraph g)
        {
            graph = g;
            dataflow_changecount = 0;
        }

        ///< Get number of data-flow changes
        public int getChangeCount() => dataflow_changecount;

        /// Run the whole algorithm
        /// Collapse everything in the control-flow graph to isolated blocks with no inputs and outputs.
        public void collapseAll()
        {
            finaltrace = false;
            graph.clearVisitCount();
            orderLoopBodies();
            collapseConditions();
            int isolated_count = collapseInternal(null);
            while (isolated_count < graph.getSize()) {
                FlowBlock targetbl = selectGoto();
                isolated_count = collapseInternal(targetbl);
            }
        }
    }
}
