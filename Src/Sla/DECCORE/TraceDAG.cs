
using System;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief Algorithm for selecting unstructured edges based an Directed Acyclic Graphs (DAG)
    ///
    /// With the exception of the back edges in loops, structured code tends to form a DAG.
    /// Within the DAG, all building blocks of structured code have a single node entry point
    /// and (at most) one exit block. Given root points, this class traces edges with this kind of
    /// structure.  Paths can recursively split at any point, starting a new \e active BranchPoint, but
    /// the BranchPoint can't be \e retired until all paths emanating from its start either terminate
    /// or come back together at the same FlowBlock node. Once a BranchPoint is retired, all the edges
    /// traversed from the start FlowBlock to the end FlowBlock are likely structurable. After pushing
    /// the traces as far as possible and retiring as much as possible, any \e active edge left
    /// is a candidate for an unstructured branch.
    ///
    /// Ultimately this produces a list of \e likely \e gotos, which is used whenever the structuring
    /// algorithm (ActionBlockStructure) gets stuck.
    ///
    /// The tracing can be restricted to a \e loopbody by setting the top FlowBlock of the loop as
    /// the root, and the loop exit block as the finish block.  Additionally, any edges that
    /// exit the loop should be marked using LoopBody::setExitMarks().
    internal class TraceDAG
    {
        /// A node in the control-flow graph with multiple outgoing edges in the DAG. Ideally, all
        /// these paths eventually merge at the same node.
        internal class BranchPoint
        {
            internal BranchPoint parent;    ///< The parent BranchPoint along which \b this is only one path
            internal int pathout;       ///< Index (of the out edge from the parent) of the path along which \b this lies
            internal FlowBlock top;     ///< FlowBlock that embodies the branch point
            internal List<BlockTrace> paths;  ///< BlockTrace for each possible path out of \b this BlockPoint
            internal int depth;         ///< Depth of BranchPoints from the root
            private bool ismark;        ///< Possible mark

            // Given the BlockTrace objects, given a new BranchPoint
            private void createTraces()
            {
                int sizeout = top.sizeOut();
                for (int i = 0; i < sizeout; ++i) {
                    if (!top.isLoopDAGOut(i)) continue;
                    paths.Add(new BlockTrace(this, paths.size(), i));
                }
            }

            // Mark a path from \b this up to the root BranchPoint
            public void markPath()
            {
                BranchPoint cur = this;
                do {
                    cur.ismark = !cur.ismark;
                    cur = cur.parent;
                } while (cur != null);
            }

            // Calculate distance between two BranchPoints
            /// The \e distance is the number of edges from \b this up to the common
            /// ancestor plus the number of edges down to the other BranchPoint.
            /// We assume that \b this has had its path up to the root marked.
            /// \param op2 is the other BranchPoint
            /// \return the distance
            public int distance(BranchPoint op2)
            {
                // find the common ancestor
                BranchPoint cur = op2;
                do {
                    if (cur.ismark) {
                        // Found the common ancestor
                        return (depth - cur.depth) + (op2.depth - cur.depth);
                    }
                    cur = cur.parent;
                } while (cur != null);
                return depth + op2.depth + 1;
            }

            // Get the start of the i-th BlockTrace
            /// Get the first FlowBlock along the i-th BlockTrace path.
            /// \param i is the index of the path
            /// \return the first FlowBlock along the path
            public FlowBlock? getPathStart(int i)
            {
                int res = 0;
                int sizeout = top.sizeOut();
                for (int j = 0; j < sizeout; ++j) {
                    if (!top.isLoopDAGOut(j)) continue;
                    if (res == i)
                        return top.getOut(j);
                    res += 1;
                }
                return (FlowBlock)null;
            }

            // Create the (unique) root branch point
            public BranchPoint()
            {
                parent = (BranchPoint)null;
                depth = 0;
                pathout = -1;
                ismark = false;
                top = (FlowBlock)null;
            }

            // Construct given a parent BlockTrace
            public BranchPoint(BlockTrace parenttrace)
            {
                parent = parenttrace.top;
                depth = parent.depth + 1;
                pathout = parenttrace.pathout;
                ismark = false;
                top = parenttrace.destnode;
                createTraces();
            }

            // BranchPoint owns its BlockTraces
            ~BranchPoint()
            {
                //for (int i = 0; i < paths.size(); ++i)
                //    delete paths[i];
            }
        }

        /// \brief A trace of a single path out of a BranchPoint
        /// Once a BranchPoint is retired with 1 outgoing edge, the multiple paths coming out of
        /// the BranchPoint are considered a single path for the parent BlockTrace.
        internal struct BlockTrace
        {
            internal enum Flags
            {
                f_active = 1,       ///< This BlockTrace is \e active.
                f_terminal = 2      ///< All paths from this point exit (without merging back to parent)
            }

            // Properties of the BlockTrace
            internal Flags flags;
            // Parent BranchPoint for which this is a path
            internal BranchPoint top;
            // Index of the out-edge for this path (relative to the parent BranchPoint)
            internal int pathout;
            // Current node being traversed along 1 path from decision point
            internal FlowBlock bottom;
            // Next FlowBlock node \b this BlockTrace will try to push into
            internal FlowBlock destnode;
            // If >1, edge to \b destnode is "virtual" representing multiple edges coming together
            internal int edgelump;
            // Position of \b this in the active trace list
            internal LinkedListNode<BlockTrace> activeiter;
            // BranchPoint blocker \b this traces into
            internal BranchPoint derivedbp;

            // Construct given a parent BranchPoint and path index
            /// \param t is the parent BranchPoint
            /// \param po is the index of the formal \e path out of the BranchPoint to \b this
            /// \param eo is the edge index out of the BranchPoints root FlowBlock
            public BlockTrace(BranchPoint t, int po, int eo)
            {
                flags = 0;
                top = t;
                pathout = po;
                bottom = top.top;
                destnode = bottom.getOut(eo);
                edgelump = 1;
                derivedbp = (BranchPoint)null;
            }

            // Construct a root BlockTrace
            /// Attach BlockTrace to a virtual root BranchPoint, where there
            /// isn't an explicit FlowBlock acting as branch point.
            /// \param root is the virtual BranchPoint
            /// \param po is the \e path out the BranchPoint to \b this
            /// \param bl is the first FlowBlock along the path
            public BlockTrace(BranchPoint root, int po, FlowBlock bl)
            {
                flags = 0;
                top = root;
                pathout = po;
                bottom = (FlowBlock)null;
                destnode = bl;
                edgelump = 1;
                derivedbp = (BranchPoint)null;
            }

            // Return \b true if \b this is active
            public bool isActive() => ((flags & Flags.f_active) != 0);

            // Return \b true is \b this terminates
            public bool isTerminal() => ((flags & Flags.f_terminal) != 0);
        }

        /// \brief Record for scoring a BlockTrace for suitability as an unstructured branch
        /// This class holds various metrics about BlockTraces that are used to sort them.
        internal struct BadEdgeScore
        {
            // Putative exit block for the BlockTrace
            internal FlowBlock exitproto;
            // The active BlockTrace being considered
            internal BlockTrace trace;
            // Minimum distance crossed by \b this and any other BlockTrace sharing same exit block
            internal int distance;
            // 1 if BlockTrace destination has no exit, 0 otherwise
            internal int terminal;
            // Number of active BlockTraces with same BranchPoint and exit as \b this
            internal int siblingedge;

            /// Compare BadEdgeScore for unstructured suitability
            /// \param op2 is the other BadEdgeScore to compare with \b this
            /// \return true if \b this is LESS likely to be the bad edge than \b op2
            internal bool compareFinal(BadEdgeScore op2)
            {
                if (siblingedge != op2.siblingedge)
                    // A bigger sibling edge is less likely to be the bad edge
                    // A sibling edge is more important than a terminal edge.  Terminal edges have the most effect on
                    // node-joined returns, which usually doesn't happen on a switch edge, whereas switch's frequently
                    // exit to a terminal node
                    return (op2.siblingedge < siblingedge);
                if (terminal != op2.terminal)
                    return (terminal < op2.terminal);
                if (distance != op2.distance)
                    return (distance < op2.distance); // Less distance between branchpoints means less likely to be bad
                return (trace.top.depth < op2.trace.top.depth); // Less depth means less likely to be bad
            }

            /// Compare for grouping
            /// Comparator for grouping BlockTraces with the same exit block and parent BranchPoint
            /// \param op2 is the other BadEdgeScore to compare to
            /// \return \b true is \b this should be ordered before \b op2
            internal static bool operator <(BadEdgeScore op1, BadEdgeScore op2)
            {
                int thisind = op1.exitproto.getIndex();
                int op2ind = op2.exitproto.getIndex();
                if (thisind != op2ind)  // Sort on exit block being traced to
                    return (thisind < op2ind);
                FlowBlock? tmpbl = op1.trace.top.top;
                thisind = (tmpbl != (FlowBlock)null) ? tmpbl.getIndex() : -1;
                tmpbl = op2.trace.top.top;
                op2ind = (tmpbl != (FlowBlock)null) ? tmpbl.getIndex() : -1;
                if (thisind != op2ind)  // Then sort on branch point being traced from
                    return (thisind < op2ind);
                thisind = op1.trace.pathout;
                op2ind = op2.trace.pathout;    // Then on the branch being taken
                return (thisind < op2ind);
            }
        }

        // A reference to the list of likely goto edges being produced
        private List<FloatingEdge> likelygoto;
        // List of root FlowBlocks to trace from
        private List<FlowBlock> rootlist = new List<FlowBlock>();
        // Current set of BranchPoints that have been traced
        private List<BranchPoint> branchlist = new List<BranchPoint>();
        // Number of active BlockTrace objects
        private int activecount;
        // Current number of active BlockTraces that can't be pushed further
        private int missedactivecount;
        // The list of \e active BlockTrace objects
        private LinkedList<BlockTrace> activetrace = new LinkedList<BlockTrace>();
        // The current \e active BlockTrace being pushed
        private LinkedListNode<BlockTrace> current_activeiter;
        // Designated exit block for the DAG (or null)
        private FlowBlock finishblock;

        /// Remove the indicated BlockTrace
        /// This adds the BlockTrace to the list of potential unstructured edges.
        /// Then patch up the BranchPoint/BlockTrace/pathout hierarchy.
        /// \param trace is the indicated BlockTrace to remove
        private void removeTrace(BlockTrace trace)
        {
            // Record that we should now treat this edge like goto
            likelygoto.Add(new FloatingEdge(trace.bottom, trace.destnode)); // Create goto record
            trace.destnode.setVisitCount(trace.destnode.getVisitCount() + trace.edgelump); // Ignore edge(s)

            BranchPoint parentbp = trace.top;

            if (trace.bottom != parentbp.top) {
                // If trace has moved past the root branch, we can treat trace as terminal
                trace.flags |= BlockTrace.Flags.f_terminal;
                trace.bottom = (FlowBlock)null;
                trace.destnode = (FlowBlock)null;
                trace.edgelump = 0;
                // Do NOT remove from active list
                return;
            }
            // Otherwise we need to actually remove the path from the BranchPoint as the root branch will be marked as a goto
            removeActive(trace);    // The trace will no longer be active
            int size = parentbp.paths.size();
            for (int i = trace.pathout + 1; i < size; ++i) {
                // Move every trace above -trace-s pathout down one slot
                BlockTrace movedtrace = parentbp.paths[i];
                movedtrace.pathout -= 1;   // Correct the trace's pathout
                BranchPoint derivedbp = movedtrace.derivedbp;
                if (derivedbp != (BranchPoint)null)
                    derivedbp.pathout -= 1;    // Correct any derived BranchPoint's pathout
                parentbp.paths[i - 1] = movedtrace;
            }
            parentbp.paths.RemoveLastItem(); // Remove the vacated slot
            // delete trace;           // Delete the record
        }

        /// \brief Process a set of conflicting BlockTrace objects that go to the same exit point.
        ///
        /// For each conflicting BlockTrace, calculate the minimum distance between it and any other BlockTrace.
        /// \param start is the beginning of the list of conflicting BlockTraces (annotated as BadEdgeScore)
        /// \param end is the end of the list of conflicting BlockTraces
        private void processExitConflict(IEnumerator<BadEdgeScore> start, IEnumerator<BadEdgeScore> end)
        {
            IEnumerator<BadEdgeScore> iter;
            BranchPoint startbp;

            while (start != end) {
                iter = start;
                ++iter;
                startbp = start.Current.trace.top;
                if (iter != end) {
                    startbp.markPath();    // Mark path to root, so we can find common ancestors easily
                    do {
                        if (startbp == iter.Current.trace.top) {
                            // Edge coming from same BranchPoint
                            start.Current.siblingedge += 1;
                            iter.Current.siblingedge += 1;
                        }
                        int dist = startbp.distance(iter.Current.trace.top);
                        // Distance is symmetric with respect to the pair of traces,
                        // Update minimum for both traces
                        if ((start.Current.distance == -1) || (start.Current.distance > dist))
                            start.Current.distance = dist;
                        if ((start.Current.distance == -1) || (start.Current.distance > dist))
                            start.Current.distance = dist;
                        ++iter;
                    } while (iter != end);
                    startbp.markPath();    // Unmark the path
                }
                if (!start.MoveNext()) break;
            }
        }

        /// Select the the most likely unstructured edge from active BlockTraces
        /// Run through the list of active BlockTrace objects, annotate them using
        /// the BadEdgeScore class, then select the BlockTrace which is the most likely
        /// candidate for an unstructured edge.
        /// \return the BlockTrace corresponding to the unstructured edge
        private BlockTrace selectBadEdge()
        {
            List<BadEdgeScore> badedgelist = new List<BadEdgeScore>();
            IEnumerator<BlockTrace> aiter = activetrace.GetEnumerator();
            while (aiter.MoveNext()) {
                if (aiter.Current.isTerminal()) continue;
                // Never remove virtual edges
                if ((aiter.Current.top.top == (FlowBlock)null) && (aiter.Current.bottom == (FlowBlock)null))
                    continue;
                BadEdgeScore newScore = new BadEdgeScore() {
                    trace = aiter.Current,
                    distance = -1,
                    siblingedge = 0
                };
                newScore.exitproto = newScore.trace.destnode;
                newScore.terminal = (newScore.trace.destnode.sizeOut() == 0) ? 1 : 0;
                badedgelist.Add(newScore);
            }
            badedgelist.Sort();

            int iterIndex = 0; // badedgelist
            int startiterIndex = iterIndex;
            FlowBlock curbl = badedgelist[iterIndex].exitproto;
            int samenodecount = 1;
            iterIndex++;
            while (iterIndex < badedgelist.Count) {
                // Find traces to the same exitblock
                BadEdgeScore score = badedgelist[iterIndex];
                if (curbl == score.exitproto) {
                    samenodecount += 1; // Count another trace to the same exit
                    ++iterIndex;
                }
                else {
                    // A new exit node
                    if (samenodecount > 1)
                        processExitConflict(badedgelist[startiterIndex], badedgelist[iterIndex]);
                    curbl = score.exitproto;
                    startiterIndex = iterIndex;
                    samenodecount = 1;
                    ++iterIndex;
                }
            }
            if (samenodecount > 1)
                // Process possible final group of traces exiting to same block
                processExitConflict(badedgelist[startiterIndex], badedgelist[iterIndex]);

            iterIndex = 0;
            int maxiterIndex = iterIndex;
            ++iterIndex;
            while (iterIndex < badedgelist.Count) {
                if (badedgelist[maxiterIndex].compareFinal(badedgelist[iterIndex]) {
                    maxiterIndex = iterIndex;
                }
                ++iterIndex;
            }
            return badedgelist[maxiterIndex].trace;
        }

        /// Move a BlockTrace into the \e active category
        /// \param trace is the BlockTrace to mark as \e active
        private void insertActive(BlockTrace trace)
        {
            LinkedListNode<BlockTrace> newBlock = new LinkedListNode<BlockTrace>(trace);
            activetrace.AddLast(trace);
            int index = activetrace.Count - 1;
            trace.activeiter = newBlock;
            trace.flags |= BlockTrace.Flags.f_active;
            activecount += 1;
        }

        ///< Remove a BlockTrace from the \e active category
        /// \param trace is the BlockTrace to be unmarked
        private void removeActive(BlockTrace trace)
        {
            activetrace.Remove(trace.activeiter);
            trace.flags &= ~BlockTrace.Flags.f_active;
            activecount -= 1;
        }

        /// Check if we can push the given BlockTrace into its next node
        /// Verify the given BlockTrace can push into the next FlowBlock (\b destnode).
        /// A FlowBlock node can only be \e opened if all the incoming edges have been traced.
        /// \param trace is the given BlockTrace to push
        /// \return \b true is the new node can be opened
        private bool checkOpen(BlockTrace trace)
        {
            if (trace.isTerminal()) return false; // Already been opened
            bool isroot = false;
            if (trace.top.depth == 0) {
                if (trace.bottom == (FlowBlock)null)
                    // Artificial root can always open its first level (edge is not real edge)
                    return true;
                isroot = true;
            }

            FlowBlock bl = trace.destnode;
            if ((bl == finishblock) && (!isroot))
                // If there is a designated exit, only the root can open it
                return false;
            int ignore = trace.edgelump + bl.getVisitCount();
            int count = 0;
            for (int i = 0; i < bl.sizeIn(); ++i) {
                if (bl.isLoopDAGIn(i)) {
                    count += 1;
                    if (count > ignore) return false;
                }
            }
            return true;
        }

        // Open a new BranchPoint along a given BlockTrace
        /// Given that a BlockTrace can be opened into its next FlowBlock node,
        /// create a new BranchPoint at that node, and set up new sub-traces.
        /// \param parent is the given BlockTrace to split
        /// \return an iterator (within the \e active list) to the new BlockTrace objects
        private LinkedListNode<BlockTrace> openBranch(BlockTrace parent)
        {
            BranchPoint newbranch = new BranchPoint(parent);
            parent.derivedbp = newbranch;
            if (newbranch.paths.size() == 0) {
                // No new traces, return immediately to parent trace
                // delete newbranch;
                parent.derivedbp = (BranchPoint)null;
                // marking it as terminal
                parent.flags |= BlockTrace.Flags.f_terminal;
                parent.bottom = (FlowBlock)null;
                parent.destnode = (FlowBlock)null;
                parent.edgelump = 0;
                return parent.activeiter;
            }
            removeActive(parent);
            branchlist.Add(newbranch);
            for (int i = 0; i < newbranch.paths.size(); ++i)
                insertActive(newbranch.paths[i]);
            return newbranch.paths[0].activeiter;
        }

        /// Check if a given BlockTrace can be retired
        /// For the given BlockTrace, make sure all other sibling BlockTraces from its
        /// BranchPoint parent either terminate or flow to the same FlowBlock node.
        /// If so, return \b true and pass back that node as the \b exitblock.
        /// \param trace is the given BlockTrace
        /// \param exitblock will hold the passed back exit block
        /// \return \b true is the BlockTrace can be retired
        private bool checkRetirement(BlockTrace trace, out FlowBlock exitblock)
        {
            exitblock = null;
            if (trace.pathout != 0) return false; // Only check, if this is the first sibling
            BranchPoint bp = trace.top;
            if (bp.depth == 0) {
                // Special conditions for retirement of root branch point
                for (int i = 0; i < bp.paths.size(); ++i) {
                    BlockTrace curtrace = bp.paths[i];
                    if (!curtrace.isActive()) return false;
                    // All root paths must be terminal
                    if (!curtrace.isTerminal()) return false;
                }
                return true;
            }
            FlowBlock? outblock = (FlowBlock)null;
            for (int i = 0; i < bp.paths.size(); ++i) {
                BlockTrace curtrace = bp.paths[i];
                if (!curtrace.isActive()) return false;
                if (curtrace.isTerminal()) continue;
                if (outblock == curtrace.destnode) continue;
                if (outblock != (FlowBlock)null) return false;
                outblock = curtrace.destnode;
            }
            exitblock = outblock;
            return true;
        }

        /// \brief Retire a BranchPoint, updating its parent BlockTrace
        /// Knowing a given BranchPoint can be retired, remove all its BlockTraces
        /// from the \e active list, and update the BranchPoint's parent BlockTrace
        /// as having reached the BlockTrace exit point.
        /// \param bp is the given BranchPoint
        /// \param exitblock is unique exit FlowBlock (calculated by checkRetirement())
        /// \return an iterator to the next \e active BlockTrace to examine
        private LinkedListNode<BlockTrace> retireBranch(BranchPoint bp, FlowBlock exitblock)
        {
            FlowBlock? edgeout_bl = (FlowBlock)null;
            int edgelump_sum = 0;

            for (int i = 0; i < bp.paths.size(); ++i) {
                BlockTrace curtrace = bp.paths[i];
                if (!curtrace.isTerminal()) {
                    edgelump_sum += curtrace.edgelump;
                    if (edgeout_bl == (FlowBlock)null)
                        edgeout_bl = curtrace.bottom;
                }
                removeActive(curtrace); // Child traces are complete and no longer active
            }
            if (bp.depth == 0)
                // If this is the root block
                // This is all there is to do
                return activetrace.First();

            if (bp.parent != (BranchPoint)null) {
                BlockTrace parenttrace = bp.parent.paths[bp.pathout];
                parenttrace.derivedbp = (BranchPoint)null; // Derived branchpoint is gone
                if (edgeout_bl == (FlowBlock)null)
                {       // If all traces were terminal
                    parenttrace.flags |= BlockTrace.Flags.f_terminal;
                    parenttrace.bottom = (FlowBlock)null;
                    parenttrace.destnode = (FlowBlock)null;
                    parenttrace.edgelump = 0;
                }
                else {
                    parenttrace.bottom = edgeout_bl;
                    parenttrace.destnode = exitblock;
                    parenttrace.edgelump = edgelump_sum;
                }
                insertActive(parenttrace); // Parent trace gets re-activated
                return parenttrace.activeiter;
            }
            return activetrace.First();
        }

        /// Clear the \b visitcount field of any FlowBlock we have modified
        /// The \b visitcount field is only modified in removeTrace() whenever we put an edge
        /// in the \b likelygoto list.
        private void clearVisitCount()
        {
            foreach (FloatingEdge edge in likelygoto)
                edge.getBottom().setVisitCount(0);
        }

        /// Construct given the container for likely unstructured edges
        /// Prepare for a new trace using the provided storage for the likely unstructured
        /// edges that will be discovered.
        /// \param lg is the container for likely unstructured edges
        public TraceDAG(List<FloatingEdge> lg)
        {
            likelygoto = lg;
            activecount = 0;
            finishblock = (FlowBlock)null;
        }

        ~TraceDAG()
        {
            //for (int i = 0; i < branchlist.size(); ++i)
            //    delete branchlist[i];
        }

        /// Add a root FlowBlock to the trace
        public void addRoot(FlowBlock root)
        {
            rootlist.Add(root);
        }

        ///< Create the initial BranchPoint and BlockTrace objects
        /// Given the registered root FlowBlocks, create the initial (virtual) BranchPoint
        /// and an associated BlockTrace for each root FlowBlock.
        public void initialize()
        {
            // Create a virtual BranchPoint for all entry points
            BranchPoint rootBranch = new BranchPoint();
            branchlist.Add(rootBranch);

            for (int i = 0; i < rootlist.size(); ++i) {
                // Find the entry points
                BlockTrace newtrace = new BlockTrace(rootBranch, rootBranch.paths.size(), rootlist[i]);
                rootBranch.paths.Add(newtrace);
                insertActive(newtrace);
            }
        }

        ///< Push the trace through, removing edges as necessary
        /// From the root BranchPoint, recursively push the trace. At any point where pushing
        /// is no longer possible, select an appropriate edge to remove and add it to the
        /// list of likely unstructured edges.  Then continue pushing the trace.
        public void pushBranches()
        {
            FlowBlock exitblock;

            current_activeiter = activetrace.First ?? throw new ApplicationException();
            missedactivecount = 0;
            while (activecount > 0) {
                if (object.ReferenceEquals(current_activeiter, activetrace.Last))
                    current_activeiter = activetrace.First;
                BlockTrace curtrace = current_activeiter.Value;
                if (missedactivecount >= activecount) {
                    // Could not push any trace further
                    BlockTrace badtrace = selectBadEdge(); // So we pick an edge to be unstructured
                    removeTrace(badtrace);  // destroy the trace
                    current_activeiter = activetrace.First;
                    missedactivecount = 0;
                }
                else if (checkRetirement(curtrace, out exitblock)) {
                    current_activeiter = retireBranch(curtrace.top, exitblock);
                    missedactivecount = 0;
                }
                else if (checkOpen(curtrace)) {
                    current_activeiter = openBranch(curtrace);
                    missedactivecount = 0;
                }
                else {
                    missedactivecount += 1;
                    current_activeiter = current_activeiter.Next ?? throw new ApplicationException();
                }
            }
            clearVisitCount();
        }

        /// Mark an exit point not to trace beyond
        public void setFinishBlock(FlowBlock bl)
        {
            finishblock = bl;
        }
    }
}
