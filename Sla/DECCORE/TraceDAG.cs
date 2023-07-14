using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
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
        internal struct BranchPoint
        {
            private BranchPoint parent;    ///< The parent BranchPoint along which \b this is only one path
            private int pathout;       ///< Index (of the out edge from the parent) of the path along which \b this lies
            private FlowBlock top;     ///< FlowBlock that embodies the branch point
            private List<BlockTrace> paths;  ///< BlockTrace for each possible path out of \b this BlockPoint
            private int depth;         ///< Depth of BranchPoints from the root
            private bool ismark;        ///< Possible mark
            private void createTraces();    ///< Given the BlockTrace objects, given a new BranchPoint

            public void markPath();    ///< Mark a path from \b this up to the root BranchPoint

            public int distance(BranchPoint op2);    ///< Calculate distance between two BranchPoints
            public FlowBlock getPathStart(int i);    ///< Get the start of the i-th BlockTrace

            public BranchPoint();      ///< Create the (unique) root branch point
            public BranchPoint(BlockTrace parenttrace);   ///< Construct given a parent BlockTrace
            
            ~BranchPoint();		///< BranchPoint owns its BlockTraces
        }

        /// \brief A trace of a single path out of a BranchPoint
        /// Once a BranchPoint is retired with 1 outgoing edge, the multiple paths coming out of
        /// the BranchPoint are considered a single path for the parent BlockTrace.
        internal struct BlockTrace
        {
            internal enum X
            {
                f_active = 1,       ///< This BlockTrace is \e active.
                f_terminal = 2      ///< All paths from this point exit (without merging back to parent)
            }
            private uint flags;        ///< Properties of the BlockTrace
            private BranchPoint top;       ///< Parent BranchPoint for which this is a path
            private int pathout;       ///< Index of the out-edge for this path (relative to the parent BranchPoint)
            private FlowBlock bottom;      ///< Current node being traversed along 1 path from decision point
            private FlowBlock destnode;    ///< Next FlowBlock node \b this BlockTrace will try to push into
            private int edgelump;      ///< If >1, edge to \b destnode is "virtual" representing multiple edges coming together
            list<BlockTrace*>::iterator activeiter; ///< Position of \b this in the active trace list
            private BranchPoint derivedbp; ///< BranchPoint blocker \b this traces into
            
            public BlockTrace(BranchPoint t, int po, int eo);       ///< Construct given a parent BranchPoint and path index

            public BlockTrace(BranchPoint root, int po, FlowBlock bl);  ///< Construct a root BlockTrace

            /// Return \b true if \b this is active
            public bool isActive() => ((flags & f_active)!=0);

            /// Return \b true is \b this terminates
            public bool isTerminal() => ((flags & f_terminal)!=0);
        }

        /// \brief Record for scoring a BlockTrace for suitability as an unstructured branch
        /// This class holds various metrics about BlockTraces that are used to sort them.
        internal struct BadEdgeScore
        {
            private FlowBlock exitproto;	///< Putative exit block for the BlockTrace
            private BlockTrace trace;		///< The active BlockTrace being considered
            private int distance;		///< Minimum distance crossed by \b this and any other BlockTrace sharing same exit block
            private int terminal;		///< 1 if BlockTrace destination has no exit, 0 otherwise
            private int siblingedge;		///< Number of active BlockTraces with same BranchPoint and exit as \b this
            
            internal bool compareFinal(BadEdgeScore op2);   ///< Compare BadEdgeScore for unstructured suitability

            internal bool operator <(BadEdgeScore op2); ///< Compare for grouping
        }

        private List<FloatingEdge> likelygoto;    ///< A reference to the list of likely goto edges being produced
        private List<FlowBlock> rootlist;        ///< List of root FlowBlocks to trace from
        private List<BranchPoint> branchlist;    ///< Current set of BranchPoints that have been traced
        private int activecount;           ///< Number of active BlockTrace objects
        private int missedactivecount;     ///< Current number of active BlockTraces that can't be pushed further
        private List<BlockTrace> activetrace;  ///< The list of \e active BlockTrace objects
        private list<BlockTrace*>::iterator current_activeiter; ///< The current \e active BlockTrace being pushed
        private FlowBlock finishblock;     ///< Designated exit block for the DAG (or null)

        private void removeTrace(BlockTrace trace);    ///< Remove the indicated BlockTrace

        private void processExitConflict(IEnumerator<BadEdgeScore> start,
            IEnumerator<BadEdgeScore> end);

        private BlockTrace selectBadEdge();    ///< Select the the most likely unstructured edge from active BlockTraces

        private void insertActive(BlockTrace trace);   ///< Move a BlockTrace into the \e active category

        private void removeActive(BlockTrace trace);   ///< Remove a BlockTrace from the \e active category

        private bool checkOpen(BlockTrace trace);  ///< Check if we can push the given BlockTrace into its next node

        private IEnumerator<BlockTrace> openBranch(BlockTrace parent); ///< Open a new BranchPoint along a given BlockTrace

        private bool checkRetirement(BlockTrace trace, FlowBlock exitblock);  ///< Check if a given BlockTrace can be retired

        private IEnumerator<BlockTrace> retireBranch(BranchPoint bp, FlowBlock exitblock);

        private void clearVisitCount();		/// Clear the \b visitcount field of any FlowBlock we have modified

        public TraceDAG(List<FloatingEdge> lg);    ///< Construct given the container for likely unstructured edges

        ~TraceDAG();            ///< Destructor

        /// Add a root FlowBlock to the trace
        public void addRoot(FlowBlock root)
        {
            rootlist.Add(root);
        }

        public void initialize();      ///< Create the initial BranchPoint and BlockTrace objects

        public void pushBranches();        ///< Push the trace through, removing edges as necessary

        /// Mark an exit point not to trace beyond
        public void setFinishBlock(FlowBlock bl)
        {
            finishblock = bl;
        }
    }
}
