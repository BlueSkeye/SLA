using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for holding an edge while the underlying graph is being manipulated
    ///
    /// The original FlowBlock nodes that define the end-points of the edge may get
    /// collapsed, but the edge may still exist between higher level components.
    /// The edge can still be retrieved via the getCurrentEdge() method.
    internal class FloatingEdge
    {
        /// Starting FlowBlock of the edge
        private FlowBlock top;
        /// Ending FlowBlock of the edge
        private FlowBlock bottom;

        /// Construct given end points
        public FloatingEdge(FlowBlock t, FlowBlock b)
        {
            top = t;
            bottom = b;
        }

        /// Get the starting FlowBlock
        public FlowBlock getTop() => top;

        /// Get the ending FlowBlock
        public FlowBlock getBottom() => bottom;

        /// Get the current form of the edge
        /// Retrieve the current edge (as a \e top FlowBlock and the index of the outgoing edge).
        /// If the end-points have been collapsed together, this returns NULL.
        /// The top and bottom nodes of the edge are updated to FlowBlocks in the current collapsed graph.
        /// \param outedge will hold the index of the edge (outgoing relative to returned FlowBlock)
        /// \param graph is the containing BlockGraph
        /// \return the current \e top of the edge or NULL
        public FlowBlock getCurrentEdge(out int outedge, FlowBlock graph)
        {
            while (top.getParent() != graph) {
                // Move up through collapse hierarchy to current graph
                top = top.getParent();
            }
            while (bottom.getParent() != graph) {
                bottom = bottom.getParent();
            }
            outedge = top.getOutIndex(bottom);
            // Edge does not exist (any longer)
            return (outedge < 0) ? null : top;
        }
    }
}
