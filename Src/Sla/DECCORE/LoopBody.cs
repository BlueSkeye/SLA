using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A description of the body of a loop.
    /// Following Tarjan, assuming there are no \e irreducible edges, a loop body is defined
    /// by the \e head (or entry-point) and 1 or more tails, which each have a \e back \e edge into
    /// the head.
    internal class LoopBody
    {
        /// head of the loop
        private FlowBlock head;
        /// (Possibly multiple) nodes with back edge returning to the head
        private List<FlowBlock> tails;
        /// Nested depth of this loop
        private int depth;
        /// Total number of unique head and tail nodes
        private int uniquecount;
        /// Official exit block from loop, or NULL
        private FlowBlock exitblock;
        /// Edges that exit to the formal exit block
        private List<FloatingEdge> exitedges;
        /// Immediately containing loop body, or NULL
        private LoopBody? immed_container;

        /// \brief Find blocks in containing loop that aren't in \b this
        /// Assuming \b this has all of its nodes marked, find all additional nodes that create the
        /// body of the \b container loop. Mark these and put them in \b body list.
        /// \param container is a loop that contains \b this
        /// \param body will hold blocks in the body of the container that aren't in \b this
        private void extendToContainer(LoopBody container, List<FlowBlock> body)
        {
            int i = 0;
            if (!container.head.isMark()) {
                // container head may already be in subloop, if not
                // add it to new body
                container.head.setMark();
                body.Add(container.head);
                // make sure we don't traverse back from it
                i = 1;
            }
            for (int j = 0; j < container.tails.Count; ++j) {
                FlowBlock tail = container.tails[j];
                if (!tail.isMark()) {
                    // container tail may already be in subloop, if not
                    tail.setMark();
                    // add to body, make sure we DO traverse back from it
                    body.Add(tail);
                }
            }
            // -this- head is already marked, but hasn't been traversed
            if (head != container.head) {
                // Unless the container has the same head, traverse the contained head
                int sizein = head.sizeIn();
                for (int k = 0; k < sizein; ++k) {
                    if (head.isGotoIn(k)) {
                        // Don't trace back through irreducible edges
                        continue;
                    }
                    FlowBlock bl = head.getIn(k);
                    if (bl.isMark()) {
                        // Already in list
                        continue;
                    }
                    bl.setMark();
                    body.Add(bl);
                }
            }
            while (i < body.Count) {
                FlowBlock curblock = body[i++];
                int sizein = curblock.sizeIn();
                for (int k = 0; k < sizein; ++k) {
                    if (curblock.isGotoIn(k)) {
                        // Don't trace back through irreducible edges
                        continue;
                    }
                    FlowBlock bl = curblock.getIn(k);
                    if (bl.isMark()) {
                        // Already in list
                        continue;
                    }
                    bl.setMark();
                    body.Add(bl);
                }
            }
        }

        /// Construct with a loop head
        public LoopBody(FlowBlock h)
        {
            head = h;
            immed_container = null;
            depth = 0;
        }

        /// Return the head FlowBlock of the loop
        public FlowBlock getHead() => head;

        /// Return current loop bounds (\b head and \b bottom).
        /// This updates the \b head and \b tail nodes to FlowBlock in the current collapsed graph.
        /// This returns the first \b tail and passes back the head.
        /// \param top is where \b head is passed back
        /// \param graph is the containing control-flow structure
        /// \return the current loop \b head
        public FlowBlock? getCurrentBounds(out FlowBlock? top, FlowBlock graph)
        {
            top = null;
            while (head.getParent() != graph) {
                // Move up through collapse hierarchy to current graph
                head = head.getParent();
            }
            FlowBlock bottom;
            for (int i = 0; i < tails.Count; ++i) {
                bottom = tails[i];
                while (bottom.getParent() != graph) {
                    bottom = bottom.getParent();
                }
                tails[i] = bottom;
                if (bottom != head) {
                    // If the loop hasn't been fully collapsed yet
                    top = head;
                    return bottom;
                }
            }
            return null;
        }

        /// Add a \e tail to the loop
        public void addTail(FlowBlock bl)
        {
            tails.Add(bl);
        }

        /// Get the exit FlowBlock or NULL
        public FlowBlock getExitBlock() => exitblock;

        /// Mark the body FlowBlocks of \b this loop
        /// Collect all FlowBlock nodes that reach a \b tail of the loop without going through \b head.
        /// Put them in a list and mark them.
        /// \param body will contain the body nodes
        public void findBase(List<FlowBlock> body)
        {
            head.setMark();
            body.Add(head);
            for (int j = 0; j < tails.Count; ++j) {
                FlowBlock tail = tails[j];
                if (!tail.isMark()) {
                    tail.setMark();
                    body.Add(tail);
                }
            }
            // Number of nodes that either head or tail
            uniquecount = body.Count;
            int i = 1;
            while (i < body.Count) {
                FlowBlock curblock = body[i++];
                int sizein = curblock.sizeIn();
                for (int k = 0; k < sizein; ++k) {
                    if (curblock.isGotoIn(k)) {
                        // Don't trace back through irreducible edges
                        continue;
                    }
                    FlowBlock bl = curblock.getIn(k);
                    if (bl.isMark()) {
                        // Already in list
                        continue;
                    }
                    bl.setMark();
                    body.Add(bl);
                }
            }
        }

        /// Extend body (to blocks that never exit)
        /// Extend the \b body of this loop to every FlowBlock that can be reached
        /// \b only from \b head without hitting the \b exitblock.
        /// Assume \b body has been filled out by findBase() and that all these blocks have their mark set.
        /// \param body contains the current loop body and will be extended
        public void extend(List<FlowBlock> body)
        {
            List<FlowBlock> trial = new List<FlowBlock>();
            int i = 0;
            while (i < body.Count) {
                FlowBlock bl = body[i++];
                int sizeout = bl.sizeOut();
                for (int j = 0; j < sizeout; ++j) {
                    if (bl.isGotoOut(j)) {
                        // Don't extend through goto edge
                        continue;
                    }
                    FlowBlock curbl = bl.getOut(j);
                    if (curbl.isMark()) {
                        continue;
                    }
                    if (curbl == exitblock) {
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
            for (i = 0; i < trial.Count; ++i) {
                // Make sure to clear the count
                trial[i].setVisitCount(0);
            }
        }

        /// Choose the exit block for \b this loop
        /// A structured loop is allowed at most one exit block: pick this block.
        /// First build a set of trial exits, preferring from a \b tail, then from  \b head,
        /// then from the middle. If there is no containing loop, just return the first such exit we find.
        /// \param body is the list FlowBlock objects in the loop body, which we assume are marked.
        public void findExit(List<FlowBlock> body)
        {
            List<FlowBlock> trialexit;
            FlowBlock tail;

            for (int j = 0; j < tails.Count; ++j) {
                tail = tails[j];
                int sizeout = tail.sizeOut();

                for (int i = 0; i < sizeout; ++i) {
                    if (tail.isGotoOut(i)) {
                        // Don't use goto as exit edge
                        continue;
                    }
                    FlowBlock curbl = tail.getOut(i);
                    if (!curbl.isMark()) {
                        if (immed_container == null) {
                            exitblock = curbl;
                            return;
                        }
                        trialexit.Add(curbl);
                    }
                }
            }

            for (int i = 0; i < body.Count; ++i) {
                FlowBlock bl = body[i];
                if ((i > 0) && (i < uniquecount)) {
                    // Filter out tails (processed previously)
                    continue;
                }
                int sizeout = bl.sizeOut();
                for (int j = 0; j < sizeout; ++j) {
                    if (bl.isGotoOut(j)) {
                        // Don't use goto as exit edge
                        continue;
                    }
                    FlowBlock curbl = bl.getOut(j);
                    if (!curbl.isMark()) {
                        if (immed_container == null) {
                            exitblock = curbl;
                            return;
                        }
                        trialexit.Add(curbl);
                    }
                }
            }
            // Default exit is null, if no block meeting condition can be found
            exitblock = null;
            if (0 == trialexit.Count) {
                return;
            }

            // If there is a containing loop, force exitblock to be in the containing loop
            if (immed_container != null) {
                List<FlowBlock> extension = new List<FlowBlock>();
                extendToContainer(immed_container, extension);
                for (int i = 0; i < trialexit.Count; ++i) {
                    FlowBlock bl = trialexit[i];
                    if (bl.isMark()) {
                        exitblock = bl;
                        break;
                    }
                }
                clearMarks(extension);
            }
        }

        /// Find preferred \b tail
        /// The idea is if there is more than one \b tail for a loop, some tails are more "preferred" than others
        /// and should have their exit edges preserved longer and be the target of the DAG path.
        /// Currently we look for a single \b tail that has an outgoing edge to the \b exitblock and
        /// make sure it is the first tail.
        public void orderTails()
        {
            if (tails.Count <= 1) {
                return;
            }
            if (exitblock == null) {
                return;
            }
            int prefindex;
            FlowBlock trial;
            for (prefindex = 0; prefindex < tails.Count; ++prefindex) {
                trial = tails[prefindex];
                int sizeout = trial.sizeOut();
                int j;
                for (j = 0; j < sizeout; ++j) {
                    if (trial.getOut(j) == exitblock) {
                        break;
                    }
                }
                if (j < sizeout) {
                    break;
                }
            }
            if (prefindex >= tails.Count()) {
                return;
            }
            if (prefindex == 0) {
                return;
            }
            // Swap preferred tail into the first position
            tails[prefindex] = tails[0];
            tails[0] = trial;
        }

        /// Label edges that exit the loop
        /// Label any edge that leaves the set of nodes in \b body.
        /// Put the edges in priority for removal,  middle exit at front, \e head exit, then \e tail exit.
        /// We assume all the FlowBlock nodes in \b body have been marked.
        /// \param body is list of nodes in \b this loop body
        public void labelExitEdges(List<FlowBlock> body)
        {
            List<FlowBlock> toexitblock = new List<FlowBlock>();
            for (int i = uniquecount; i < body.Count(); ++i) {
                // For non-head/tail nodes of graph
                FlowBlock curblock = body[i];
                int sizeout = curblock.sizeOut();
                for (int k = 0; k < sizeout; ++k) {
                    if (curblock.isGotoOut(k)) {
                        // Don't exit through goto edges
                        continue;
                    }
                    FlowBlock bl = curblock.getOut(k);
                    if (bl == exitblock) {
                        toexitblock.Add(curblock);
                        // Postpone exit to exitblock
                        continue;
                    }
                    if (!bl.isMark()) {
                        exitedges.Add(new FloatingEdge(curblock, bl));
                    }
                }
            }
            if (head != null) {
                int sizeout = head.sizeOut();
                for (int k = 0; k < sizeout; ++k) {
                    if (head.isGotoOut(k)) {
                        // Don't exit through goto edges
                        continue;
                    }
                    FlowBlock bl = head.getOut(k);
                    if (bl == exitblock) {
                        toexitblock.Add(head);
                        // Postpone exit to exitblock
                        continue;
                    }
                    if (!bl.isMark()) {
                        exitedges.Add(new FloatingEdge(head, bl));
                    }
                }
            }
            for (int i = tails.Count - 1; i >= 0; --i) {
                // Put exits from more preferred tails later
                FlowBlock curblock = tails[i];
                if (curblock == head) {
                    continue;
                }
                int sizeout = curblock.sizeOut();
                for (int k = 0; k < sizeout; ++k) {
                    if (curblock.isGotoOut(k)) {
                        // Don't exit through goto edges
                        continue;
                    }
                    FlowBlock bl = curblock.getOut(k);
                    if (bl == exitblock) {
                        toexitblock.Add(curblock);
                        // Postpone exit to exitblock
                        continue;
                    }
                    if (!bl.isMark()) {
                        exitedges.Add(new FloatingEdge(curblock, bl));
                    }
                }
            }
            for (int i = 0; i < toexitblock.Count; ++i) {
                // Now we do exits to exitblock
                FlowBlock bl = toexitblock[i];
                exitedges.Add(new FloatingEdge(bl, exitblock));
            }
        }

        /// \brief Record any loops that \b body contains.
        ///
        /// Search for any loop contained by \b this and update is \b depth and \b immed_container field.
        /// \param body is the set of FlowBlock nodes making up this loop
        /// \param looporder is the list of known loops
        public void labelContainments(List<FlowBlock> body, List<LoopBody> looporder)
        {
            List<LoopBody> containlist = new List<LoopBody>();

            for (int i = 0; i < body.Count; ++i) {
                FlowBlock curblock = body[i];
                if (curblock != head) {
                    LoopBody subloop = LoopBody.find(curblock, looporder);
                    if (subloop != null) {
                        containlist.Add(subloop);
                        subloop.depth += 1;
                    }
                }
            }
            // Note the following code works even though the depth fields may shift during subsequent calls to this routine
            // Once a LoopBody calls this routine
            //    the depth of any contained loop will permanently be bigger than this LoopBody
            //       because any other loop will either
            //         increment the depth of both this LoopBody and any loop that it contains   OR
            //         increment neither the LoopBody  nor a loop it contains  OR
            //         NOT increment the LoopBody but DO increment a contained loop
            // So when the immediate container a of loop b calls this routine
            //         a has a depth greater than any containing LoopBody that has already run
            //         =>  therefore b->immed_container->depth < a->depth    and  a claims the immed_container position
            // Subsequent containers c of a and b, will have c->depth < a->depth because c contains a
            for (int i = 0; i < containlist.Count; ++i) {
                // Keep track of the most immediate container
                LoopBody lb = containlist[i];
                if ((lb.immed_container == null) || (lb.immed_container.depth < depth)) {
                    lb.immed_container = this;
                }
            }
        }

        /// Collect likely \e unstructured edges
        /// Add edges that exit from \b this loop body to the list of likely \e gotos,
        /// giving them the proper priority.
        /// \param likely will hold the exit edges in (reverse) priority order
        /// \param graph is the containing control-flow graph
        public void emitLikelyEdges(List<FloatingEdge> likely, FlowBlock graph)
        {
            while (head.getParent() != graph) {
                head = head.getParent();
            }
            if (exitblock != null) {
                while (exitblock.getParent() != graph) {
                    exitblock = exitblock.getParent();
                }
            }
            for (int i = 0; i < tails.Count; ++i) {
                FlowBlock tail = tails[i];
                while (tail.getParent() != graph) {
                    tail = tail.getParent();
                }
                tails[i] = tail;
                if (tail == exitblock) {
                    // If the exitblock was collapsed into the tail, we no longer really have an exit
                    exitblock = null;
                }
            }
            IEnumerator<FloatingEdge> iter = exitedges.GetEnumerator();
            bool completed = !iter.MoveNext();
            FlowBlock? holdin = null;
            FlowBlock? holdout = null;
            while (!completed) {
                int outedge;
                FlowBlock inbl = iter.Current.getCurrentEdge(outedge, graph);
                completed = !iter.MoveNext();
                if (inbl == null) {
                    continue;
                }
                FlowBlock outbl = inbl.getOut(outedge);
                if (completed) {
                    if (outbl == exitblock) {
                        // If this is the official exit edge
                        // Hold off putting the edge in list
                        holdin = inbl;
                        holdout = outbl;
                        break;
                    }
                }
                likely.Add(new FloatingEdge(inbl, outbl));
            }
            for (int i = tails.Count - 1; i >= 0; --i) {
                // Go in reverse order, to put out less preferred back-edges first
                if ((holdin != null) && (i == 0)) {
                    // Put in delayed exit, right before final backedge
                    likely.Add(new FloatingEdge(holdin, holdout));
                }
                FlowBlock tail = tails[i];
                int sizeout = tail.sizeOut();
                for (int j = 0; j < sizeout; ++j) {
                    FlowBlock bl = tail.getOut(j);
                    if (bl == head) {
                        // If out edge to head (back-edge for this loop)
                        // emit it
                        likely.Add(new FloatingEdge(tail, head));
                    }
                }
            }
        }

        /// Mark all the exits to this loop
        /// Exit edges have their f_loop_exit_edge property set.
        /// \param graph is the containing control-flow structure
        public void setExitMarks(FlowBlock graph)
        {
            foreach (FloatingEdge iter in exitedges) {
                int outedge;
                FlowBlock? inloop = iter.getCurrentEdge(outedge, graph);
                if (inloop != null) {
                    inloop.setLoopExit(outedge);
                }
            }
        }

        /// Clear the mark on all the exits to this loop
        /// This clears the f_loop_exit_edge on any edge exiting \b this loop.
        /// \param graph is the containing control-flow structure
        public void clearExitMarks(FlowBlock graph)
        {
            foreach (FloatingEdge iter in exitedges) {
                int outedge;
                FlowBlock inloop = iter.getCurrentEdge(outedge, graph);
                if (inloop != null) {
                    inloop.clearLoopExit(outedge);
                }
            }
        }

        public static bool operator <(LoopBody op1, LoopBody op2)
        {
            return (op1.depth > op2.depth);
        }
        
        ///< Order loop bodies by depth
        /// Merge loop bodies that share the same \e head
        /// Look for LoopBody records that share a \b head. Merge each \b tail
        /// from one into the other. Set the merged LoopBody \b head to NULL,
        /// for later clean up.
        /// \param looporder is the list of LoopBody records
        public static void mergeIdenticalHeads(List<LoopBody> looporder)
        {
            int i = 0;
            int j = i + 1;

            LoopBody curbody = looporder[i];
            while (j < looporder.Count) {
                LoopBody nextbody = looporder[j++];
                if (nextbody.head == curbody.head) {
                    curbody.addTail(nextbody.tails[0]);
                    // Mark this LoopBody as subsumed
                    nextbody.head = null;
                }
                else {
                    i += 1;
                    looporder[i] = nextbody;
                    curbody = nextbody;
                }
            }
            // Total size of merged array
            i += 1;
            looporder.resize(i);
        }

        /// Compare the \b head then \b tail
        /// Compare two loops based on the indices of the \b head and then the \e tail.
        /// \param a is the first LoopBody to compare
        /// \param b is the second LoopBody to compare
        /// \return \b true if the first LoopBody comes before the second
        public static bool compare_ends(LoopBody a, LoopBody b)
        {
            int aindex = a.head.getIndex();
            int bindex = b.head.getIndex();
            if (aindex != bindex) {
                return (aindex < bindex);
            }
            // Only compare the first tail
            aindex = a.tails[0].getIndex();
            bindex = b.tails[0].getIndex();
            return (aindex < bindex);
        }

        /// Compare just the \b head
        /// Compare two loops based on the indices of the \b head
        /// \param a is the first LoopBody to compare
        /// \param looptop is the second
        /// \return -1,0, or 1 if the first is ordered before, the same, or after the second
        public static int compare_head(LoopBody a, FlowBlock looptop)
        {
            int aindex = a.head.getIndex();
            int bindex = looptop.getIndex();
            return (aindex != bindex) ? ((aindex < bindex) ? -1 : 1) : 0;
        }

        /// Find a LoopBody
        /// Given the top FlowBlock of a loop, find corresponding LoopBody record from an ordered list.
        /// This assumes mergeIdenticalHeads() has been run so that the head is uniquely identifying.
        /// \param looptop is the top of the loop
        /// \param looporder is the ordered list of LoopBody records
        /// \return the LoopBody or NULL if none found
        public static LoopBody? find(FlowBlock looptop, List<LoopBody> looporder)
        {
            int min = 0;
            int max = looporder.Count - 1;
            while (min <= max) {
                int mid = (min + max) / 2;
                int comp = compare_head(looporder[mid], looptop);
                if (comp == 0) {
                    return looporder[mid];
                }
                if (comp < 0) {
                    min = mid + 1;
                }
                else {
                    max = mid - 1;
                }
            }
            return null;
        }

        /// Clear the body marks
        /// \param body is the list of FlowBlock nodes that have been marked
        public static void clearMarks(List<FlowBlock> body)
        {
            for (int i = 0; i < body.Count; ++i) {
                body[i].clearMark();
            }
        }
    }
}
