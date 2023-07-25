using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Priority queue for the phi-node (MULTIEQUAL) placement algorithm
    ///
    /// A \e work-list for basic blocks used during phi-node placement.  Implemented as
    /// a set of stacks with an associated priority.  Blocks are placed in the \e queue
    /// with an associated \e priority (or depth) using the insert() method.  The current
    /// highest priority block is retrieved with the extract() method.
    internal class PriorityQueue
    {
        /// An array of \e stacks, indexed by priority
        private List<List<FlowBlock>> queue;
        /// The current highest priority index with active blocks
        private int4 curdepth;
        
        public PriorityQueue()
        {
            curdepth = -2;
        }

        /// Reset to an empty queue
        /// Any basic blocks currently in \b this queue are removed. Space is
        /// reserved for a new set of prioritized stacks.
        /// \param maxdepth is the number of stacks to allocate
        public void reset(int4 maxdepth)
        {
            if ((curdepth == -1) && (maxdepth == queue.size() - 1)) return; // Already reset
            queue.clear();
            queue.resize(maxdepth + 1);
            curdepth = -1;
        }

        /// Insert a block into the queue given its priority
        /// The block is pushed onto the stack of the given priority.
        /// \param bl is the block being added to the queue
        /// \param depth is the priority to associate with the block
        public void insert(FlowBlock bl, int4 depth)
        {
            queue[depth].push_back(bl);
            if (depth > curdepth)
                curdepth = depth;
        }

        /// Retrieve the highest priority block
        /// The block at the top of the highest priority non-empty stack is popped
        /// and returned.  This will always return a block. It shouldn't be called if the
        /// queue is empty.
        /// \return the highest priority block
        public FlowBlock extract()
        {
            FlowBlock* res = queue[curdepth].back();
            queue[curdepth].pop_back();
            while (queue[curdepth].empty())
            {
                curdepth -= 1;
                if (curdepth < 0) break;
            }
            return res;
        }

        /// Return \b true if \b this queue is empty
        public bool empty() => (curdepth==-1);
    }
}
