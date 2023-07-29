using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.FlowBlock;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A control-flow block built out of sub-components
    /// This is the core class for building a hierarchy of control-flow blocks.
    /// A set of control-flow blocks can be grouped together and viewed as a single block,
    /// with its own input and output blocks.
    /// All the code structuring elements (BlockList, BlockIf, BlockWhileDo, etc.) derive from this.
    internal class BlockGraph : FlowBlock
    {
        /// List of FlowBlock components within \b this super-block
        private List<FlowBlock> list = new List<FlowBlock>();

        /// Add a component FlowBlock
        /// Add the given FlowBlock to the list and make \b this the parent
        /// Update \b index so that it has the minimum over all components
        /// \param bl is the given FlowBlock
        private void addBlock(FlowBlock bl)
        {
            int min = bl.index;

            if (0 == list.Count) {
                index = min;
            }
            else {
                if (min < index) {
                    index = min;
                }
            }
            bl.parent = this;
            list.Add(bl);
        }

        /// Force number of outputs
        /// Force \b this FlowBlock to have the indicated number of outputs.
        /// Create edges back into itself if necessary.
        /// \param i is the number of out edges to force
        private void forceOutputNum(int i)
        {
#if BLOCKCONSISTENT_DEBUG
            if (sizeOut() > i) {
                throw new LowlevelError("Bad block output force");
            }
#endif
            while (sizeOut() < i) {
                addInEdge(this, f_loop_edge | f_back_edge);
            }
        }

        /// Inherit our edges from the edges of our components
        /// Examine the set of components and their incoming and outgoing edges.  If both
        /// ends of the edge are not within the set, then \b this block inherits the edge.
        /// A formal BlockEdge is added between \b this and the FlowBlock outside the set.
        /// The edges are deduplicated.
        private void selfIdentify()
        {
            FlowBlock mybl;
            FlowBlock otherbl;

            if (0 == list.Count) {
                return;
            }
            foreach (FlowBlock iter in list) {
                mybl = iter;
                int i = 0;
                while (i < mybl.intothis.size()) {
                    otherbl = mybl.intothis[i].point;
                    if (otherbl.parent == this) {
                        i += 1;
                    }
                    else {
                        for (int j = 0; j < otherbl.outofthis.size(); ++j) {
                            if (otherbl.outofthis[j].point == mybl) {
                                otherbl.replaceOutEdge(j, this);
                            }
                        }
                        // Dont increment i
                    }
                }
                i = 0;
                while (i < mybl.outofthis.size()) {
                    otherbl = mybl.outofthis[i].point;
                    if (otherbl.parent == this) {
                        i += 1;
                    }
                    else {
                        for (int j = 0; j < otherbl.intothis.size(); ++j) {
                            if (otherbl.intothis[j].point == mybl) {
                                otherbl.replaceInEdge(j, this);
                            }
                        }
                        if (mybl.isSwitchOut()) {
                            // Check for indirect branch out
                            setFlag(f_switch_out);
                        }
                    }
                }
            }
            dedup();
        }

        /// \brief Move nodes from \b this into a new BlockGraph
        /// This does most of the work of collapsing a set of components in \b this
        /// into a single node. The components are removed from \b this, put in the new FlowBlock
        /// and adjusts edges. The new FlowBlock must be added back into \b this.
        /// \param ident is the new FlowBlock
        /// \param nodes is the list component FlowBlocks to move
        private void identifyInternal(BlockGraph ident, List<FlowBlock> nodes)
        {
            foreach (FlowBlock iter in nodes) {
#if BLOCKCONSISTENT_DEBUG
                if (iter.parent != this) {
                    throw new LowlevelError("Bad block identify");
                }
#endif
                iter.setMark();
                // Maintain order of blocks
                ident.addBlock(iter);
                ident.flags |= (iter.flags & (f_interior_gotoout | f_interior_gotoin));
            }
            foreach (FlowBlock iter in list) {
                // Remove -nodes- from our list
                if (!iter.isMark()) {
                    newlist.Add(iter);
                }
                else {
                    iter.clearMark();
                }
            }
            list = newlist;
            ident.selfIdentify();
        }

        /// Clear a set of properties from all edges in the graph
        /// \param fl is the set of boolean properties
        private void clearEdgeFlags(uint fl)
        {
            fl = ~fl;
            int size = list.Count;
            for (int j = 0; j < size; ++j) {
                FlowBlock bl = list[j];
                for (int i = 0; i < bl.intothis.Count; ++i) {
                    bl.intothis[i].label &= fl;
                }
                for (int i = 0; i < bl.outofthis.Count; ++i) {
                    bl.outofthis[i].label &= fl;
                }
            }
        }

        /// \brief Create a single root block
        /// Some algorithms need a graph with a single entry node. Given multiple entry points,
        /// this routine creates an artificial root with no \e in edges and an \e out
        /// edge to each of the real entry points.  The resulting root FlowBlock isn't
        /// owned by any BlockGraph, and the caller is responsible for freeing it.
        /// \param rootlist is the given set of entry point FlowBlocks
        /// \return the new artificial root FlowBlock
        private static FlowBlock createVirtualRoot(List<FlowBlock> rootlist)
        {
            FlowBlock newroot = new FlowBlock();
            for (int i = 0; i < rootlist.Count; ++i) {
                rootlist[i].addInEdge(newroot, 0);
            }
            return newroot;
        }

        /// \brief Find a spanning tree (skipping irreducible edges).
        ///   - Label pre and reverse-post orderings, tree, forward, cross, and back edges.
        ///   - Calculate number of descendants.
        ///   - Put the blocks of the graph in reverse post order.
        ///   - Return an array of all nodes in pre-order.
        ///   - If the graph does not have a real root, create one and return it, otherwise return null.
        /// Algorithm originally due to Tarjan.
        /// The first block is the entry block, and should remain the first block
        /// \param preorder will hold the list of FlowBlock components in pre-order
        /// \param rootlist will hold the list of entry points
        private void findSpanningTree(List<FlowBlock> preorder, List<FlowBlock> rootlist)
        {
            if (list.Count == 0) {
                return;
            }
            vector<FlowBlock> rpostorder;
            List<FlowBlock> state = new List<FlowBlock>();
            List<int> istate = new List<int>();
            FlowBlock tmpbl;
            int origrootpos;

            preorder.reserve(list.Count);
            rpostorder.resize(list.Count);
            state.reserve(list.Count);
            istate.reserve(list.Count);
            for (int i = 0; i < list.Count; ++i) {
                tmpbl = list[i];
                // reverse post-order starts at 0
                tmpbl.index = -1;
                tmpbl.visitcount = -1;
                tmpbl.copymap = tmpbl;
                if (tmpbl.sizeIn() == 0) {
                    // Keep track of all potential roots of the tree
                    rootlist.Add(tmpbl);
                }
            }
            if (rootlist.Count > 1) {
                // Make sure orighead is visited last, (so it is first in the reverse post order)
                tmpbl = rootlist[rootlist.Count - 1];
                rootlist[rootlist.Count - 1] = rootlist[0];
                rootlist[0] = tmpbl;
            }
            else if (rootlist.Count == 0) {
                // If there's no obvious starting block
                // Assume first block is entry point
                rootlist.Add(list[0]);
            }
            // Position of original head in rootlist
            origrootpos = rootlist.Count - 1;

            for (int repeat = 0; repeat < 2; ++repeat) {
                bool extraroots = false;
                int rpostcount = list.Count;
                int rootindex = 0;
                // Clear all edge flags
                clearEdgeFlags(~uint.MaxValue);
                while (preorder.Count < list.Count) {
                    FlowBlock? startbl = null;
                    while (rootindex < rootlist.Count) {
                        // Go thru blocks with no in edges
                        startbl = rootlist[rootindex];
                        rootindex += 1;
                        if (startbl.visitcount == -1) {
                            break;
                        }
                        // If we reach here, startbl isn't really a root (root from previous pass)
                        for (int i = rootindex; i < rootlist.Count; ++i) {
                            rootlist[i - 1] = rootlist[i];
                        }
                        // Remove it
                        rootlist.RemoveAt(rootlist.Count - 1);
                        rootindex -= 1;
                        startbl = null;
                    }
                    if (startbl == null) {
                        // If we didn't find one, just take next unvisited
                        extraroots = true;
                        for (int i = 0; i < list.Count; ++i) {
                            startbl = list[i];
                            if (startbl.visitcount == -1) {
                                break;
                            }
                        }
                        // We have to treat this block as another root
                        rootlist.Add(startbl);
                        // Update root traversal state
                        rootindex += 1;
                    }

                    state.Add(startbl);
                    istate.Add(0);
                    startbl.visitcount = preorder.Count;
                    preorder.Add(startbl);
                    startbl.numdesc = 1;

                    while (0 != state.Count) {
                        FlowBlock curbl = state[state.Count - 1];
                        if (curbl.sizeOut() <= istate.back()) {
                            // We've visited all children of this node
                            state.RemoveAt(state.Count - 1);
                            istate.RemoveAt(istate.Count - 1);
                            rpostcount -= 1;
                            curbl.index = rpostcount;
                            rpostorder[rpostcount] = curbl;
                            if (0 != state.Count)
                                state[state.Count - 1].numdesc += curbl.numdesc;
                        }
                        else {
                            int edgenum = istate[istate.Count - 1];
                            // Next visit to this state should try next child
                            istate[istate.Count - 1] += 1;
                            if (curbl.isIrreducibleOut(edgenum)) {
                                // Pretend irreducible edges don't exist
                                continue;
                            }
                            // New child to try
                            FlowBlock childbl = curbl.getOut(edgenum);

                            if (childbl.visitcount == -1) {
                                // If we haven't visited this node before
                                curbl.setOutEdgeFlag(edgenum, f_tree_edge);
                                state.Add(childbl);
                                istate.Add(0);
                                childbl.visitcount = preorder.Count;
                                preorder.Add(childbl);
                                childbl.numdesc = 1;
                            }
                            else if (childbl.index == -1) {
                                // childbl is already on stack
                                curbl.setOutEdgeFlag(edgenum, f_back_edge | f_loop_edge);
                            }
                            else if (curbl.visitcount < childbl.visitcount) {
                                // childbl processing is already done
                                curbl.setOutEdgeFlag(edgenum, f_forward_edge);
                            }
                            else {
                                curbl.setOutEdgeFlag(edgenum, f_cross_edge);
                            }
                        }
                    }
                }
                if (!extraroots) {
                    break;
                }
                if (repeat == 1) {
                    throw new LowlevelError("Could not generate spanning tree");
                }

                // We had extra roots we did not know about so we have to regenerate the post order so entry block comes first
                tmpbl = rootlist[rootlist.Count - 1];
                // Move entry block to last position in rootlist
                rootlist[rootlist.Count - 1] = rootlist[origrootpos];
                rootlist[origrootpos] = tmpbl;

                for (int i = 0; i < list.Count; ++i) {
                    tmpbl = list[i];
                    // reverse post-order starts at 0
                    tmpbl.index = -1;
                    tmpbl.visitcount = -1;
                    tmpbl.copymap = tmpbl;
                }
                preorder.Clear();
                state.Clear();
                istate.Clear();
            }

            if (rootlist.Count > 1) {
                // Make sure orighead is at the front of the rootlist as well
                tmpbl = rootlist[rootlist.Count - 1];
                rootlist[rootlist.Count - 1] = rootlist[0];
                rootlist[0] = tmpbl;
            }
            list = rpostorder;
        }

        /// \brief Identify irreducible edges
        /// Assuming the spanning tree has been properly labeled using findSpanningTree(),
        /// test for and label and irreducible edges (the test ignores any edges already labeled as irreducible).
        /// Return \b true if the spanning tree needs to be rebuilt, because one of the tree edges is irreducible.
        /// Original algorithm due to Tarjan.
        /// \param preorder is the list of FlowBlocks in pre-order
        /// \param irreduciblecount will hold the number of irreducible edges
        /// \return true if the spanning tree needs to be rebuilt
        private bool findIrreducible(List<FlowBlock> preorder, int irreduciblecount)
        {
            // The current reachunder set being built (also with mark set on each block)
            List<FlowBlock> reachunder = new List<FlowBlock>();
            bool needrebuild = false;
            int xi = preorder.Count - 1;
            while (xi >= 0) {
                // For each vertex in reverse pre-order
                FlowBlock x = preorder[xi];
                xi -= 1;
                int sizein = x.sizeIn();
                for (int i = 0; i < sizein; ++i) {
                    if (!x.isBackEdgeIn(i)) {
                        // For each back-edge into x
                        continue;
                    }
                    FlowBlock y = x.getIn(i);
                    if (y == x) {
                        // Reachunder set does not include the loop head
                        continue;
                    }
                    // Add FIND(y) to reachunder
                    reachunder.Add(y.copymap);
                    y.copymap->setMark();
                }
                int q = 0;
                while (q < reachunder.Count) {
                    FlowBlock t = reachunder[q];
                    q += 1;
                    int sizein_t = t.sizeIn();
                    for (int i = 0; i < sizein_t; ++i) {
                        if (t.isIrreducibleIn(i)) {
                            // Pretend irreducible edges don't exist
                            continue;
                        }
                        // All back-edges into t have already been collapsed, so this is 
                        FlowBlock y = t.getIn(i); // For each forward, tree, or cross edge
                        FlowBlock yprime = y.copymap; // y' = FIND(y)
                        if ((x.visitcount > yprime.visitcount) || (x.visitcount + x.numdesc <= yprime.visitcount)) {
                            // The original Tarjan algorithm reports reducibility failure at this point
                            irreduciblecount += 1;
                            int edgeout = t.getInRevIndex(i);
                            y.setOutEdgeFlag(edgeout, f_irreducible);
                            if (t.isTreeEdgeIn(i)) {
                                // If a tree edge is irreducible, we need to rebuild the spanning tree
                                needrebuild = true;
                            }
                            else {
                                // Otherwise we can pretend the edge was already marked irreducible
                                y.clearOutEdgeFlag(edgeout, f_cross_edge | f_forward_edge);
                            }
                        }
                        else if ((!yprime.isMark()) && (yprime != x)) {
                            // if yprime is not in reachunder and not equal to x
                            reachunder.Add(yprime);
                            yprime.setMark();
                        }
                    }
                }
                // Collapse reachunder into a single node, labeled as x
                for (int i = 0; i < reachunder.Count; ++i) {
                    FlowBlock s = reachunder[i];
                    s.clearMark();
                    s.copymap = x;
                }
                reachunder.Clear();
            }
            return needrebuild;
        }

        /// Force the \e false out edge to go to the given FlowBlock
        /// Make sure \b this has exactly 2 out edges and the first edge flows to the given FlowBlock.
        /// Swap the edges if necessary. Throw an exception if this is not possible.
        /// \param out0 is the given FlowBlock
        private void forceFalseEdge(FlowBlock out0)
        {
            if (sizeOut() != 2) {
                throw new LowlevelError("Can only preserve binary condition");
            }
            if (out0.getParent() == this) {
                // Allow for loops to self
                out0 = this;
            }
            if (outofthis[0].point != out0) {
                swapEdges();
            }

            if (outofthis[0].point != out0) {
                throw new LowlevelError("Unable to preserve condition");
            }
        }

        /// Swap the positions two component FlowBlocks
        /// \param i is the position of the first FlowBlock to swap
        /// \param j is the position of the second
        protected void swapBlocks(int i, int j)
        {
            FlowBlock bl = list[i];
            list[i] = list[j];
            list[j] = bl;
        }

        /// Set properties on the first leaf FlowBlock
        /// For the given BlockGraph find the first component leaf FlowBlock and
        /// set its properties
        /// \param bl is the given BlockGraph
        /// \param fl is the property to set
        protected static void markCopyBlock(FlowBlock bl, uint fl)
        {
            bl.getFrontLeaf().flags |= fl;
        }

        /// Clear all component FlowBlock objects
        public void clear()
        {
            foreach (FlowBlock iter in list) {
                // delete* iter;
            }
            list.Clear();
        }

        /// Destructor
        ~BlockGraph()
        {
            clear();
        }

        /// Get the list of component FlowBlock objects
        public List<FlowBlock> getList() => list;

        /// Get the number of components
        public int getSize() => list.Count;

        /// Get the i-th component
        public FlowBlock getBlock(int i)
        {
            return list[i] ;
        }

        public override block_type getType() => t_graph;

        public override FlowBlock subBlock(int i)
        {
            return list[i] ;
        }

        public override void markUnstructured()
        {
            foreach (FlowBlock iter in list) {
                // Recurse
                iter.markUnstructured();
            }
        }

        public override void markLabelBumpUp(bool bump)
        {
            // Mark ourselves if true
            base.markLabelBumpUp(bump);
            if (0 == list.Count) {
                return;
            }
            IEnumerator<FlowBlock> iter = list.GetEnumerator();
            // Only pass true down to first subblock
            if (!iter.MoveNext()) {
                throw new BugException();
            }
            iter.Current.markLabelBumpUp(bump);
            while(iter.MoveNext()) {
                iter.markLabelBumpUp(false);
            }
        }

        public override void scopeBreak(int curexit, int curloopexit)
        {
            int ind;

            IEnumerator<FlowBlock> iter = list.GetEnumerator();
            bool endReached = iter.MoveNext();
            while (!endReached) {
                FlowBlock curbl = iter.Current;
                endReached = !iter.MoveNext();
                ind = endReached ? curexit : iter.Current.getIndex();
                // Recurse the scopeBreak call, making sure we pass the appropriate exit index.
                curbl.scopeBreak(ind, curloopexit);
            }
        }

        public override void printTree(TextWriter s, int level)
        {
            base.printTree(s, level);
            foreach (FlowBlock iter in list) {
                iter.printTree(s, level + 1);
            }
        }

        public override void printRaw(TextWriter s)
        {
            printHeader(s);
            s.WriteLine();
            foreach (FlowBlock iter in list) {
                iter.printRaw(s);
            }
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockGraph(this);
        }

        public override FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            IEnumerator<FlowBlock> iter = list.GetEnumerator();
            while (iter.MoveNext()) {
                if (iter == bl) {
                    break;
                }
            }
            // Find the first block after bl
            if (!iter.MoveNext()) {
                if (null == getParent()) {
                    return null;
                }
                return getParent().nextFlowAfter(this);
            }
            // The next block after bl (to be emitted)
            FlowBlock nextbl = iter.Current;
            if (null != nextbl) {
                nextbl = nextbl.getFrontLeaf();
            }
            return nextbl;
        }

        public override void finalTransform(Funcdata data)
        {
            // Recurse into all the substructures
            foreach (FlowBlock iter in list) {
                iter.finalTransform(data);
            }
        }

        public virtual void finalizePrinting(Funcdata data)
        {
            // Recurse into all the substructures
            foreach (FlowBlock iter in list) {
                iter.finalizePrinting(data);
            }
        }

        public virtual void encodeBody(Encoder encoder)
        {
            base.encodeBody(encoder);
            for (int i = 0; i < list.Count; ++i) {
                FlowBlock bl = list[i];
                encoder.openElement(ElementId.ELEM_BHEAD);
                encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, bl.getIndex());
                FlowBlock.block_type bt = bl.getType();
                string nm;
                if (bt == FlowBlock.block_type.t_if) {
                    switch(((BlockGraph)bl).getSize()) {
                        case 1:
                            nm = "ifgoto";
                            break;
                        case 2:
                            nm = "properif";
                            break;
                        default:
                            nm = "ifelse";
                            break;
                    }
                }
                else {
                    nm = FlowBlock.typeToName(bt);
                }
                encoder.writeString(AttributeId.ATTRIB_TYPE, nm);
                encoder.closeElement(ElementId.ELEM_BHEAD);
            }
            for (int i = 0; i < list.Count; ++i) {
                list[i].encode(encoder);
            }
        }

        public virtual void decodeBody(Decoder decoder)
        {
            BlockMap newresolver;
            List<FlowBlock> tmplist = new List<FlowBlock>();

            while (true) {
                uint subId = decoder.peekElement();
                if (subId != ElementId.ELEM_BHEAD) {
                    break;
                }
                decoder.openElement();
                int newindex = decoder.readSignedInteger(AttributeId.ATTRIB_INDEX);
                FlowBlock bl = newresolver.createBlock(decoder.readString(AttributeId.ATTRIB_TYPE));
                // Need to set index here for sort
                bl.index = newindex;
                tmplist.Add(bl);
                decoder.closeElement(subId);
            }
            newresolver.sortList();

            for (int i = 0; i < tmplist.Count; ++i) {
                FlowBlock bl = tmplist[i];
                bl.decode(decoder, newresolver);
                addBlock(bl);
            }
        }

        /// Decode \b this BlockGraph from a stream
        /// Parse a \<block> element.  This is currently just a wrapper around the
        /// FlowBlock::decode() that sets of the BlockMap resolver
        /// \param decoder is the stream decoder
        public void decode(Decoder decoder)
        {
            BlockMap resolver;
            base.decode(decoder, resolver);
            // Restore goto references here
        }

        /// Add a directed edge between component FlowBlocks
        /// \param begin is the start FlowBlock
        /// \param end is the stop FlowBlock
        public void addEdge(FlowBlock begin, FlowBlock end)
        {
#if BLOCKCONSISTENT_DEBUG
            if ((begin.parent != this) || (end.parent != this)) {
                throw new LowlevelError("Bad edge create");
            }
#endif
            end.addInEdge(begin, 0);
        }

        /// Mark a given edge as a \e loop edge
        /// \param begin is a given component FlowBlock
        /// \param outindex is the index of the \e out edge to mark as a loop
        public void addLoopEdge(FlowBlock begin, int outindex)
        {
#if BLOCKCONSISTENT_DEBUG
            //if ((begin->parent != this)||(end->parent != this))
            if ((begin.parent != this)) {
                throw new LowlevelError("Bad loopedge create");
            }
#endif
            //  int4 i;
            //  i = begin->OutIndex(end);
            // using OutIndex did not necessarily get the right edge
            // if there were multiple outedges to the same block
            begin.setOutEdgeFlag(outindex, f_loop_edge);
        }

        /// Remove an edge between component FlowBlocks
        /// The edge must already exist
        /// \param begin is the incoming FlowBlock of the edge
        /// \param end is the outgoing FlowBlock
        public void removeEdge(FlowBlock begin, FlowBlock end)
        {
#if BLOCKCONSISTENT_DEBUG
            if ((begin.parent != this) || (end.parent != this)) {
                throw new LowlevelError("Bad edge remove");
            }
#endif
            int i;
            for (i = 0; i < end.intothis.size(); ++i) {
                if (end.intothis[i].point == begin) {
                    break;
                }
            }
            end.removeInEdge(i);
        }

        /// Move an edge from one out FlowBlock to another
        /// The edge from \b in to \b outbefore must already exist.  It will get removed
        /// and replaced with an edge from \b in to \b outafter.  The new edge index
        /// will be the same as the removed edge, and all other edge ordering will be preserved.
        /// \param in is the input FlowBlock
        /// \param outbefore is the initial output FlowBlock
        /// \param outafter is the new output FlowBlock
        public void switchEdge(FlowBlock @in, FlowBlock outbefore, FlowBlock outafter)
        {
            for (int i = 0; i < @in.outofthis.Count; ++i) {
                if (@in.outofthis[i].point == outbefore) {
                    @in.replaceOutEdge(i, outafter);
                }
            }
        }

        /// Move indicated \e out edge to a new FlowBlock
        /// Given an edge specified by its input FlowBlock, replace that
        /// input with new FlowBlock.
        /// \param blold is the original input FlowBlock
        /// \param slot is the index of the \e out edge of \b blold
        /// \param blnew is the FlowBlock that will become the input to the edge
        public void moveOutEdge(FlowBlock blold, int slot, FlowBlock blnew)
        {
#if BLOCKCONSISTENT_DEBUG
            if ((blold.parent != this) || (blnew.parent != this)) {
                throw new LowlevelError("Bad edge move");
            }
#endif
            FlowBlock outbl = blold.getOut(slot);
            int i = blold.getOutRevIndex(slot);
            outbl.replaceInEdge(i, blnew);
        }

        /// Remove a FlowBlock from \b this BlockGraph
        /// The indicated block is pulled out of the component list and deleted.
        /// Any edges between it and the rest of the BlockGraph are simply removed.
        /// \param bl is the indicated block
        public void removeBlock(FlowBlock bl)
        {
#if BLOCKCONSISTENT_DEBUG
            if (bl.parent != this) {
                throw new LowlevelError("Bad block remove");
            }
#endif
            //vector<FlowBlock*>::iterator iter;
            while (bl.sizeIn() > 0) {
                // Rip the block out of the graph
                removeEdge(bl.getIn(0), bl);
            }
            while (bl.sizeOut() > 0) {
                removeEdge(bl, bl.getOut(0));
            }

            List<FlowBlock> removedBlocks = new List<FlowBlock>();
            foreach (FlowBlock iter in list) {
                if (iter == bl) {
                    removedBlocks.Add(iter);
                    break;
                }
            }
            foreach(FlowBlock removedBlock in removedBlocks) {
                list.Remove(removedBlock);
            }
            // Free up memory
            /// delete bl;
        }

        /// Remove given FlowBlock preserving flow in \b this
        /// This should be applied only if the given FlowBlock has 0 or 1 outputs.
        /// If there is an output FlowBlock, all incoming edges to the given FlowBlock
        /// are moved so they flow into the output FlowBlock, then all remaining edges
        /// into or out of the given FlowBlock are removed.  The given FlowBlock is \b not
        /// removed from \b this.
        /// This routine doesn't preserve loopedge information
        /// \param bl is the given FlowBlock component
        public void removeFromFlow(FlowBlock bl)
        {
#if BLOCKCONSISTENT_DEBUG
            if (bl.parent != this) {
                throw new LowlevelError("Bad remove from flow");
            }
            if ((bl.sizeIn() > 0) && (bl.sizeOut() > 1)) {
                throw new LowlevelError("Illegal remove from flow");
            }
#endif
            FlowBlock bbout;
            FlowBlock bbin;
            while (bl.sizeOut() > 0) {
                bbout = bl.getOut(bl.sizeOut() - 1);
                bl.removeOutEdge(bl.sizeOut() - 1);
                while (bl.sizeIn() > 0) {
                    bbin = bl.getIn(0);
                    bbin.replaceOutEdge(bl.intothis[0].reverse_index, bbout);
                }
            }
        }

        /// Remove FlowBlock splitting flow between input and output edges
        /// Remove the given FlowBlock from the flow of the graph. It must have
        /// 2 inputs, and 2 outputs.  The edges will be remapped so that
        ///   - In(0) -> Out(0) and
        ///   - In(1) -> Out(1)
        ///
        /// Or if \b flipflow is true:
        ///   - In(0) -> Out(1)
        ///   - In(1) -> Out(0)
        /// \param bl is the given FlowBlock
        /// \param flipflow indicates how the edges are remapped
        public void removeFromFlowSplit(FlowBlock bl, bool flipflow)
        {
#if BLOCKCONSISTENT_DEBUG
            if (bl.parent != this) {
                throw new LowlevelError("Bad remove from flow split");
            }
            if ((bl.sizeIn() != 2) && (bl.sizeOut() != 2)) {
                throw new LowlevelError("Illegal remove from flow split");
            }
#endif
            if (flipflow) {
                // Replace edge slot from 0 -> 1
                bl.replaceEdgesThru(0, 1);
            }
            else {
                // Replace edge slot from 1 -> 1
                bl.replaceEdgesThru(1, 1);
            }
            // Removing the first edge
            // Replace remaining edge
            bl.replaceEdgesThru(0, 0);
        }

        /// Splice given FlowBlock together with its output
        /// The given FlowBlock must have exactly one output.  That output must have
        /// exactly one input.  The output FlowBlock is removed and any outgoing edges
        /// it has become outgoing edge of the given FlowBlock.  The output FlowBlock
        /// is permanently removed. It is viewed as being \e spliced together with the given FlowBlock.
        /// \param bl is the given FlowBlock
        public void spliceBlock(FlowBlock bl)
        {
            FlowBlock? outbl = null;
            if (bl.sizeOut() == 1) {
                outbl = bl.getOut(0);
                if (outbl.sizeIn() != 1) {
                    outbl = null;
                }
            }
            if (null == outbl) {
                throw new LowlevelError(
                    "Can only splice a block with 1 output to a block with 1 input");
            }
            // Flags from the input block that we keep
            uint fl1 = bl.flags & (f_unstructured_targ | f_entry_point);
            // Flags from the output block that we keep
            uint fl2 = outbl.flags & f_switch_out;
            bl.removeOutEdge(0);
            // Move every out edge of -outbl- to -bl-
            int szout = outbl.sizeOut();
            for (int i = 0; i < szout; ++i) {
                moveOutEdge(outbl, 0, bl);
            }
            removeBlock(outbl);
            bl.flags = fl1 | fl2;
        }

        /// Set the entry point FlowBlock for \b this graph
        /// The component list is reordered to make the given FlowBlock first.
        /// The \e f_entry_point property is updated.
        /// \param bl is the given FlowBlock to make the entry point
        public void setStartBlock(FlowBlock bl)
        {
#if BLOCKCONSISTENT_DEBUG
            if (bl.parent != this) {
                throw new LowlevelError("Bad set start");
            }
#endif
            if ((list[0].flags & f_entry_point) != 0) {
                if (bl == list[0]) {
                    // Already set as start block
                    return;
                }
                // Remove old entry point
                list[0].flags &= ~f_entry_point;
            }

            int i;
            for (i = 0; i < list.size(); ++i) {
                if (list[i] == bl) {
                    break;
                }
            }

            for (int j = i; j > 0; --j) {
                // Slide everybody down
                list[j] = list[j - 1];
            }
            list[0] = bl;
            bl.flags |= f_entry_point;
        }

        /// Get the entry point FlowBlock
        /// Throw an exception if no entry point is registered
        /// \return the entry point FlowBlock
        public FlowBlock getStartBlock()
        {
            if (list.empty() || ((list[0].flags & f_entry_point) == 0)) {
                throw new LowlevelError("No start block registered");
            }
            return list[0];
        }

        // Factory functions
        /// Build a new plain FlowBlock
        /// Add the new FlowBlock to \b this
        /// \return the new FlowBlock
        public FlowBlock newBlock()
        {
            FlowBlock ret = new FlowBlock();
            addBlock(ret);
            return ret;
        }

        /// Build a new BlockBasic
        /// Add the new BlockBasic to \b this
        /// \param fd is the function underlying the basic block
        /// \return the new BlockBasic
        public BlockBasic newBlockBasic(Funcdata fd)
        {
            BlockBasic ret = new BlockBasic(fd);
            addBlock(ret);
            return ret;
        }

        // Factory (identify) routines
        /// Build a new BlockCopy
        /// Add the new BlockCopy to \b this
        /// \param bl is the FlowBlock underlying the copy
        /// \return the new BlockCopy
        public BlockCopy newBlockCopy(FlowBlock bl)
        {
            BlockCopy ret = new BlockCopy(bl) {
                intothis = bl.intothis,
                outofthis = bl.outofthis,
                immed_dom = bl.immed_dom,
                index = bl.index
            };
            // visitcount needs to be initialized to zero via FlowBlock constructure
            //  ret->visitcount = bl->visitcount;
            ret.numdesc = bl.numdesc;
            ret.flags |= bl.flags;
            if (ret.outofthis.size() > 2) {
                // Make sure switch is marked (even if not produced by INDIRECT) as it is needed for structuring
                ret.flags |= f_switch_out;
            }
            addBlock(ret);
            return ret;
        }

        /// Build a new BlockGoto
        /// Add the new BlockGoto to \b this, incorporating the given FlowBlock
        /// \param bl is the given FlowBlock whose outgoing edge is to be marked as a \e goto
        /// \return the new BlockGoto
        public BlockGoto newBlockGoto(FlowBlock bl)
        {
            BlockGoto ret = new BlockGoto(bl.getOut(0));
            List<FlowBlock> nodes = new List<FlowBlock>();
            nodes.Add(bl);
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(1);
            // Treat out edge as if it didn't exist
            removeEdge(ret, ret.getOut(0));
            return ret;
        }

        /// Build a new BlockMultiGoto
        /// The given FlowBlock may already be a BlockMultiGoto, otherwise we
        /// add the new BlockMultiGoto to \b this.
        /// \param bl is the given FlowBlock with the new \e goto edge
        /// \param outedge is the index of the outgoing edge to make into a \e goto
        /// \return the (possibly new) BlockMultiGoto
        public BlockMultiGoto newBlockMultiGoto(FlowBlock bl, int outedge)
        {
            BlockMultiGoto ret;
            FlowBlock targetbl = bl.getOut(outedge);
            bool isdefaultedge = bl.isDefaultBranch(outedge);
            if (bl.getType() == t_multigoto) {
                // Already one goto edge from this same block, we add to existing structure
                ret = (BlockMultiGoto)bl;
                ret.addEdge(targetbl);
                removeEdge(ret, targetbl);
                if (isdefaultedge) {
                    ret.setDefaultGoto();
                }
            }
            else {
                ret = new BlockMultiGoto(bl);
                List<FlowBlock> nodes = new List<FlowBlock>();
                nodes.Add(bl);
                identifyInternal(ret, nodes);
                addBlock(ret);
                ret.addEdge(targetbl);
                if (targetbl != bl) {
                    // If the target is itself, edge is already removed by identifyInternal
                    removeEdge(ret, targetbl);
                }
                if (isdefaultedge) {
                    ret.setDefaultGoto();
                }
            }
            return ret;
        }

        /// Build a new BlockList
        /// Add the new BlockList to \b this, collapsing the given FlowBlock components into it.
        /// \param nodes is the given set of FlowBlocks components
        /// \return the new BlockList
        public BlockList newBlockList(List<FlowBlock> nodes)
        {
            FlowBlock? out0 = null;
            int outforce = nodes[nodes.Count - 1].sizeOut();
            if (outforce == 2) {
                out0 = nodes[nodes.Count - 1].getOut(0);
            }
            BlockList ret = new BlockList();
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(outforce);
            if (ret.sizeOut() == 2) {
                // Preserve the condition
                ret.forceFalseEdge(out0);
            }
            //  if ((ret->OutSize() == 2)&&(nodes.back()->Out(0) == nodes.front()))
            //    ret->FlowBlock::negateCondition(); // Preserve out ordering of last block
            return ret;
        }

        /// Build a new BlockCondition
        /// Add the new BlockCondition to \b this, collapsing its pieces into it.
        /// \param b1 is the first FlowBlock piece
        /// \param b2 is the second FlowBlock piece
        /// \return the new BlockCondition
        public BlockCondition newBlockCondition(FlowBlock b1, FlowBlock b2)
        {
            FlowBlock out0 = b2.getOut(0);
            List<FlowBlock> nodes = new List<FlowBlock>();
            OpCode opc = (b1.getFalseOut() == b2) ? CPUI_INT_OR : CPUI_INT_AND;
            BlockCondition ret = new BlockCondition(opc);
            nodes.Add(b1);
            nodes.Add(b2);
            identifyInternal(ret, nodes);
            addBlock(ret);
            // All conditions must have two outputs
            ret.forceOutputNum(2);
            // Preserve the condition
            ret.forceFalseEdge(out0);
            return ret;
        }

        /// Build a new BlockIfGoto
        /// Add the new BlockIfGoto to \b this, collapsing the given condition FlowBlock into it.
        /// \param cond is the given condition FlowBlock
        /// \return the new BlockIfGoto
        public BlockIf newBlockIfGoto(FlowBlock cond)
        {
            if (!cond.isGotoOut(1)) {
                // True branch must be a goto branch
                throw new LowlevelError("Building ifgoto where true branch is not the goto");
            }
            FlowBlock out0 = cond.getOut(0);
            List<FlowBlock> nodes = new List<FlowBlock>();
            BlockIf ret = new BlockIf();
            // Store the target
            ret.setGotoTarget(cond.getOut(1));
            nodes.Add(cond);
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(2);
            // Preserve the condition
            ret.forceFalseEdge(out0);
            // Treat the true edge as if it didn't exist
            removeEdge(ret, ret.getTrueOut());
            return ret;
        }

        /// Build a new BlockIf
        /// Add the new BlockIf to \b this, collapsing the condition and body FlowBlocks into it.
        /// \param cond is the condition FlowBlock
        /// \param tc is the body FlowBlock
        /// \return the new BlockIf
        public BlockIf newBlockIf(FlowBlock cond, FlowBlock tc)
        {
            List<FlowBlock> nodes = new List<FlowBlock>();
            BlockIf ret = new BlockIf();
            nodes.Add(cond);
            nodes.Add(tc);
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(1);
            return ret;
        }

        /// Build a new BlockIfElse
        /// Add the new BlockIfElse to \b this, collapsing the condition, true clause, and false clause into it.
        /// \param cond is the condition FlowBlock
        /// \param tc is the true clause FlowBlock
        /// \param fc is the false clause FlowBlock
        /// \return the new BlockIf
        public BlockIf newBlockIfElse(FlowBlock cond, FlowBlock tc, FlowBlock fc)
        {
            List<FlowBlock> nodes = new List<FlowBlock>();
            BlockIf ret = new BlockIf();
            nodes.Add(cond);
            nodes.Add(tc);
            nodes.Add(fc);
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(1);
            return ret;
        }

        /// Build a new BlockWhileDo
        /// Add the new BlockWhileDo to \b this, collapsing the condition and clause into it.
        /// \param cond is the condition FlowBlock
        /// \param cl is the clause FlowBlock
        /// \return the new BlockWhileDo
        public BlockWhileDo newBlockWhileDo(FlowBlock cond, FlowBlock cl)
        {
            List<FlowBlock> nodes = new List<FlowBlock>();
            BlockWhileDo ret = new BlockWhileDo();
            nodes.Add(cond);
            nodes.Add(cl);
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(1);
            return ret;
        }

        /// Build a new BlockDoWhile
        /// Add the new BlockDoWhile to \b this, collapsing the condition clause FlowBlock into it.
        /// \param condcl is the condition clause FlowBlock
        /// \return the new BlockDoWhile
        public BlockDoWhile newBlockDoWhile(FlowBlock condcl)
        {
            List<FlowBlock> nodes = new List<FlowBlock>();
            BlockDoWhile ret = new BlockDoWhile();
            nodes.Add(condcl);
            identifyInternal(ret, nodes);
            addBlock(ret);
            ret.forceOutputNum(1);
            return ret;
        }

        /// Build a new BlockInfLoop
        /// Add the new BlockInfLoop to \b this, collapsing the body FlowBlock into it.
        /// \param body is the body FlowBlock
        /// \return the new BlockInfLoop
        public BlockInfLoop newBlockInfLoop(FlowBlock body)
        {
            List<FlowBlock> nodes = new List<FlowBlock>();
            BlockInfLoop ret = new BlockInfLoop();
            nodes.Add(body);
            identifyInternal(ret, nodes);
            addBlock(ret);
            return ret;
        }

        /// Build a new BlockSwitch
        /// Add the new BlockSwitch to \b this, collapsing all the case FlowBlocks into it.
        /// \param cs is the list of case FlowBlocks
        /// \param hasExit is \b true if the switch has a formal exit
        /// \return the new BlockSwitch
        public BlockSwitch newBlockSwitch(List<FlowBlock> cs, bool hasExit)
        {
            FlowBlock rootbl = cs[0];
            BlockSwitch ret = new BlockSwitch(rootbl);
            FlowBlock? leafbl = rootbl.getExitLeaf();
            if ((null == leafbl)|| (leafbl.getType() != FlowBlock::t_copy)) {
                throw new LowlevelError("Could not get switch leaf");
            }
            // Must be called before the identifyInternal
            ret.grabCaseBasic(leafbl.subBlock(0), cs);
            identifyInternal(ret, cs);
            addBlock(ret);
            if (hasExit) {
                // If there is an exit, there should be exactly 1 out edge
                ret.forceOutputNum(1);
            }
            // Don't consider this as being a switch "out"
            ret.clearFlag(f_switch_out);
            return ret;
        }

        public void orderBlocks()
        {
            ///< Sort blocks using the final ordering
            if (list.Count != 1) {
                list.Sort(compareFinalOrder);
            }
        }

        /// Build a copy of a BlockGraph
        /// Construct a copy of the given BlockGraph in \b this.  The nodes of the copy
        /// will be official BlockCopy objects which will contain a reference to their
        /// corresponding FlowBlock in the given graph.  All edges will be duplicated.
        /// \param graph is the given BlockGraph to copy
        public void buildCopy(BlockGraph graph)
        {
            BlockCopy copyblock;
            int startsize = list.size();

            foreach (FlowBlock iter in graph.list) {
                copyblock = newBlockCopy(iter);
                // Store map basic->copy
                iter.copymap = copyblock;
            }
            foreach (FlowBlock iter in list) {
                iter.replaceUsingMap();
            }
        }

        /// Clear the visit count in all node FlowBlocks
        public void clearVisitCount()
        {
            foreach (FlowBlock block in list) {
                block.visitcount = 0;
            }
        }

        /// Calculate forward dominators
        /// Calculate the immediate dominator for each FlowBlock node in \b this BlockGraph,
        /// for forward control-flow.
        /// The algorithm must be provided a list of entry points for the graph.
        /// We assume the blocks are in reverse post-order and this is reflected in the index field.
        /// Using an algorithm by Cooper, Harvey, and Kennedy.
        /// Softw. Pract. Exper. 2001; 4: 1-10
        /// \param rootlist is the list of entry point FlowBlocks
        public void calcForwardDominator(List<FlowBlock> rootlist)
        {
            List<FlowBlock> postorder = new List<FlowBlock>();
            FlowBlock? virtualroot;
            FlowBlock b;
            FlowBlock? new_idom;
            FlowBlock rho;
            bool changed;
            int i, j, finger1, finger2;

            if (0 == list.Count) {
                return;
            }
            int numnodes = list.Count - 1;
            postorder.resize(list.Count);
            for (i = 0; i < list.Count; ++i) {
                // Clear the dominator field
                list[i].immed_dom = null;
                // Construct a forward post order list
                postorder[numnodes - i] = list[i];
            }
            if (rootlist.Count > 1) {
                virtualroot = createVirtualRoot(rootlist);
                postorder.Add(virtualroot);
            }
            else {
                virtualroot = null;
            }

            // The official start node
            b = postorder[postorder.Count - 1];
            if (b.sizeIn() != 0) {
                // Root node must have no in edges
                if ((rootlist.Count != 1) || (rootlist[0] != b)) {
                    throw new LowlevelError("Problems finding root node of graph");
                }
                // Create virtual root with no in edges
                virtualroot = createVirtualRoot(rootlist);
                postorder.Add(virtualroot);
                b = virtualroot;
            }
            b.immed_dom = b;
            for (i = 0; i < b.sizeOut(); ++i) {
                // Fill in dom of nodes which start immediately
                // connects to (to deal with possible artificial edge)
                b.getOut(i).immed_dom = b;
            }
            changed = true;
            new_idom = null;
            while (changed) {
                changed = false;
                for (i = postorder.Count - 2; i >= 0; --i) {
                    // For all nodes, in reverse post-order, except root
                    b = postorder[i];
                    if (b.immed_dom != postorder[postorder.Count - 1]) {
                        for (j = 0; j < b.sizeIn(); ++j) {
                            // Find first processed node
                            new_idom = b.getIn(j);
                            if (new_idom.immed_dom != null) {
                                break;
                            }
                        }
                        j += 1;
                        for (; j < b.sizeIn(); ++j) {
                            rho = b.getIn(j);
                            if (rho.immed_dom != null) {
                                // Here is the intersection routine
                                finger1 = numnodes - rho.index;
                                finger2 = numnodes - new_idom.index;
                                while (finger1 != finger2) {
                                    while (finger1 < finger2) {
                                        finger1 = numnodes - postorder[finger1].immed_dom.index;
                                    }
                                    while (finger2 < finger1) {
                                        finger2 = numnodes - postorder[finger2].immed_dom.index;
                                    }
                                }
                                new_idom = postorder[finger1];
                            }
                        }
                        if (b.immed_dom != new_idom) {
                            b.immed_dom = new_idom;
                            changed = true;
                        }
                    }
                }
            }
            if (virtualroot != null) {
                // If there was a virtual root, excise it from the dominator tree
                for (i = 0; i < list.Count; ++i) {
                    if (postorder[i].immed_dom == virtualroot) {
                        // Remove the dominator link to virtualroot
                        postorder[i].immed_dom = null;
                    }
                }
                while (virtualroot.sizeOut() > 0) {
                    // Remove any edges from virtualroot
                    virtualroot.removeOutEdge(virtualroot.sizeOut() - 1);
                }
                // delete virtualroot;
            }
            else {
                postorder[postorder.Count - 1].immed_dom = null;
            }
        }

        /// Build the dominator tree
        /// Associate dominator children with each node via a list (of lists) indexed by the FlowBlock index.
        /// \param child is the initially empty list of lists
        public void buildDomTree(List<List<FlowBlock>> child)
        {
            FlowBlock bl;

            child.Clear();
            child.resize(list.Count + 1);
            for (int i = 0; i < list.Count; ++i) {
                bl = list[i];
                if (bl.immed_dom != null) {
                    child[bl.immed_dom.index].Add(bl);
                }
                else {
                    child[list.Count].Add(bl);
                }
            }
        }

        /// Calculate dominator depths
        /// Associate every FlowBlock node in \b this graph with its depth in the dominator tree.
        /// The dominator root has depth 1, the nodes it immediately dominates have depth 2, etc.
        /// \param depth is array that will be populated with depths
        /// \return the maximum depth across all nodes
        public int buildDomDepth(List<int> depth)
        {
            FlowBlock bl;
            int max = 0;

            depth.resize(list.Count + 1);
            for (int i = 0; i < list.Count; ++i) {
                bl = list[i].immed_dom;
                depth[i] = (bl != null) ? depth[bl.getIndex()] + 1 : 1;
                if (max < depth[i]) {
                    max = depth[i];
                }
            }
            depth[list.Count] = 0;
            return max;
        }

        /// Collect nodes from a dominator sub-tree
        /// Collect all nodes in the dominator sub-tree starting at a given root FlowBlock.
        /// We assume blocks in are reverse post order.
        /// \param res will hold the list of nodes in the sub-tree
        /// \param root is the given root FlowBlock
        public void buildDomSubTree(List<FlowBlock> res, FlowBlock root)
        {
            FlowBlock bl;
            FlowBlock dombl;
            int rootindex = root.getIndex();
            res.Add(root);
            for (int i = rootindex + 1; i < list.Count; ++i) {
                bl = list[i];
                dombl = bl.getImmedDom();
                if (dombl == null) {
                    break;
                }
                if (dombl.getIndex() > rootindex) {
                    break;
                }
                res.Add(bl);
            }
        }

        /// Calculate loop edges
        /// This algorithm identifies a set of edges such that,
        /// if the edges are removed, the remaining graph has NO directed cycles
        /// The algorithm works as follows:
        /// Starting from the start block, do a depth first search through the "out" edges
        /// of the block.  If the outblock is already on the current path from root to node,
        /// we have found a cycle, we add the last edge to the list and continue pretending
        /// that edge didn't exist.  If the outblock is not on the current path but has
        /// been visited before, we can truncate the search.
        /// This is now only applied as a failsafe if the graph has irreducible edges.
        public void calcLoop()
        {
            // Look for directed cycles in graph
            // Mark edges (loopedges) that can be removed
            // to prevent looping
            vector<FlowBlock*>::iterator iter;
            FlowBlock bl;
            FlowBlock nextbl;
            int i;

            if (0 == list.Count) {
                // Nothing to do
                return;
            }

            // Current depth first path
            List<FlowBlock> path = new List<FlowBlock>();
            List<int> state = new List<int>();

            path.Add(list[0]);
            // No children visited yet
            state.Add(0);
            // Mark this node as visited and on path
            list[0].setFlag(f_mark | f_mark2);
            while (0 != path.Count) {
                bl = path[path.Count - 1];
                i = state[state.Count - 1];
                if (i >= bl.sizeOut()) {
                    // Visited everything below this node, POP
                    // Mark this node as no longer on the path
                    bl.clearFlag(f_mark2);
                    path.RemoveAt(path.Count - 1);
                    state.RemoveAt(state.Count - 1);
                }
                else {
                    state[state.Count - 1] += 1;
                    if (bl.isLoopOut(i)) {
                        // Previously marked loop-edge (act as if it doesn't exist)
                        continue;
                    }
                    nextbl = bl.getOut(i);
                    if ((nextbl.flags & f_mark2) != 0) {
                        // We found a cycle!
                        // Technically we should never reach here if the reducibility algorithms work
                        addLoopEdge(bl, i);
                        //	throw new LowlevelError("Found a new loop despite irreducibility");
                    }
                    else if ((nextbl.flags & f_mark) == 0) {
                        // Fresh node
                        nextbl.setFlag(f_mark | f_mark2);
                        path.Add(nextbl);
                        state.Add(0);
                    }
                }
            }
            foreach (FlowBlock iter in list) {
                // Clear our marks
                iter.clearFlag(f_mark | f_mark2);
            }
        }

        /// Collect reachable/unreachable FlowBlocks from a given start FlowBlock
        /// If the boolean \b un is \b true, collect unreachable blocks. Otherwise
        /// collect reachable blocks.
        /// \param res will hold the reachable or unreachable FlowBlocks
        /// \param bl is the starting FlowBlock
        /// \param un toggles reachable,unreachable
        public void collectReachable(List<FlowBlock> res, FlowBlock bl, bool un)
        {
            FlowBlock blk;
            FlowBlock blk2;

            bl.setMark();
            res.Add(bl);
            int total = 0;

            // Propagate forward to find all reach blocks from entry point
            while (total < res.Count) {
                blk = res[total++];
                for (int j = 0; j < blk.sizeOut(); ++j) {
                    blk2 = blk.getOut(j);
                    if (blk2.isMark()) {
                        continue;
                    }
                    blk2.setMark();
                    res.Add(blk2);
                }
            }
            if (un) {
                // Anything not marked is unreachable
                res.clear();
                for (int i = 0; i < list.Count; ++i) {
                    blk = list[i];
                    if (blk.isMark()) {
                        blk.clearMark();
                    }
                    else {
                        res.Add(blk);
                    }
                }
            }
            else {
                for (int i = 0; i < res.Count; ++i) {
                    res[i].clearMark();
                }
            }
        }

        /// Label loop edges
        /// - Find irreducible edges
        /// - Find a spanning tree
        /// - Set FlowBlock indices in reverse-post order
        /// - Label tree-edges, forward-edges, cross-edges, and back-edges
        /// \param rootlist will contain the entry points for the graph
        public void structureLoops(List<FlowBlock> rootlist)
        {
            List<FlowBlock> preorder = new List<FlowBlock>();
            bool needrebuild;
            int irreduciblecount = 0;

            do {
                needrebuild = false;
                findSpanningTree(preorder, rootlist);
                needrebuild = findIrreducible(preorder, irreduciblecount);
                if (needrebuild) {
                    clearEdgeFlags(f_tree_edge | f_forward_edge | f_cross_edge | f_back_edge | f_loop_edge); // Clear the spanning tree
                    preorder.Clear();
                    rootlist.Clear();
                }
            } while (needrebuild);
            if (irreduciblecount > 0) {
                // We need some kind of check here, like calcLoop, to make absolutely sure removing the loop edges makes a DAG
                calcLoop();
            }
        }

#if BLOCKCONSISTENT_DEBUG
        /// Check consistency of \b this BlockGraph
        public bool isConsistent()
        {
            FlowBlock *bl1,*bl2;
            int4 i,j,k;
            int4 count1,count2;

            for(i=0;i<list.size();++i) {
                bl1 = list[i];
                for(j=0;j<bl1.sizeIn();++j) {
                    bl2 = bl1.getIn(j);		// For each in edge
                    count1 = 0;
                    for(k=0;k<bl1.sizeIn();++k) {
                        if (bl1.getIn(k)==bl2) {
                            count1 += 1;
                        }
                    }
                    count2 = 0;
                    for(k=0;k<bl2.sizeOut();++k) {
                        if (bl2.getOut(k)==bl1) {
                            count2 += 1;
                        }
                    }
                    if (count1 != count2) {
                        return false;
                    }
                }
                for(j=0;j<bl1->sizeOut();++j) {
                    // Similarly for each out edge
                    bl2 = bl1->getOut(j);
                    count1 = 0;
                    for(k=0;k<bl1->sizeOut();++k) {
                        if (bl1->getOut(k)==bl2) { 
                            count1 += 1;
                        }
                    }
                    count2 = 0;
                    for(k=0;k<bl2->sizeIn();++k) {
                        if (bl2->getIn(k)==bl1) {
                            count2 += 1;
                        }
                    }
                    if (count1 != count2) {
                        return false;
                    }
                }
            }
            return true;
        }
#endif
    }
}
