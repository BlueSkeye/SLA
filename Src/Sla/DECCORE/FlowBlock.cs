using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Description of a control-flow block containing PcodeOps
    /// This is the base class for basic blocks (BlockBasic) and the
    /// hierarchical description of \e structured code.  At all levels,
    /// these can be viewed as a block of code (PcodeOp objects) with
    /// other blocks flowing into and out of it.
    internal class FlowBlock
    {
        // friend class BlockGraph;
        /// \brief The possible block types
        public enum block_type
        {
            t_plain,
            t_basic,
            t_graph,
            t_copy,
            t_goto,
            t_multigoto,
            t_ls,
            t_condition,
            t_if,
            t_whiledo,
            t_dowhile,
            t_switch,
            t_infloop
        }

        /// \brief Boolean properties of blocks
        /// The first four flags describe attributes of the blocks primary exiting edges
        /// The f_interior_* flags do not necessarily apply to these edges. They are used
        /// with the block structure and hierarchy algorithms where unstructured jumps
        /// are removed from the list of primary edges. These flags keep track only of
        /// the existence of unstructured edges, even though they aren't listed <summary>
        /// \brief Boolean properties of blocks
        /// </summary>
        [Flags()]
        public enum block_flags
        {
            /// (Block ends in) non-structured branch
            f_goto_goto = 1,
            /// Block ends with a break;
            f_break_goto = 2,
            /// Block ends with a continue;
            f_continue_goto = 4,
            /// Output is decided by switch
            f_switch_out = 0x10,
            /// Block is destination of unstructured goto
            f_unstructured_targ = 0x20,
            /// Generic way to mark a block
            f_mark = 0x80,
            /// A secondary mark
            f_mark2 = 0x100,
            /// Official entry point of the function
            f_entry_point = 0x200,
            /// The block has an unstructured jump out of interior
            f_interior_gotoout = 0x400,
            /// Block is target of unstructured jump to its interior
            f_interior_gotoin = 0x800,
            /// Any label printed higher up in hierarchy
            f_label_bumpup = 0x1000,
            /// Block does nothing in infinite loop (halt)
            f_donothing_loop = 0x2000,
            /// Block is in process of being deleted
            f_dead = 0x4000,
            /// Set if the conditional block of a whiledo is too big to print as while(cond) { ...
            f_whiledo_overflow = 0x8000,
            /// If true, out edges have been flipped since last time path was traced
            f_flip_path = 0x10000,
            /// Block is a merged form of original basic blocks
            f_joined_block = 0x20000,
            /// Block is a duplicated version of an original basic block
            f_duplicate_block = 0x40000
        }

        /// \brief Boolean properties on edges
        [Flags()]
        public enum edge_flags
        {
            /// Edge is unstructured
            f_goto_edge = 1,
            /// Edge completes a loop, removing these edges gives you a DAG
            f_loop_edge = 2,
            /// This is default edge from switchblock
            f_defaultswitch_edge = 4,
            /// Edge which must be removed to make graph reducible
            f_irreducible = 8,
            /// An edge in the spanning tree
            f_tree_edge = 0x10,
            /// An edge that jumps forward in the spanning tree
            f_forward_edge = 0x20,
            /// An edge that crosses subtrees in the spanning tree
            f_cross_edge = 0x40,
            /// Within (reducible) graph, a back edge defining a loop
            f_back_edge = 0x80,
            /// Edge exits the body of a loop
            f_loop_exit_edge = 0x100
        }

        /// Collection of block_flags
        private block_flags flags;
        /// The parent block to which \b this belongs
        private FlowBlock parent;
        /// Immediate dominating block
        private FlowBlock immed_dom;
        /// Back reference to a BlockCopy of \b this
        private FlowBlock copymap;
        /// Reference index for this block (reverse post order)
        private int index;
        /// A count of visits of this node for various algorithms
        private int visitcount;
        /// Number of descendants of this block in spanning tree (+1)
        private int numdesc;
        /// Blocks which (can) fall into this block
        private List<BlockEdge> intothis = new List<BlockEdge>();
        /// Blocks into which this block (can) fall
        private List<BlockEdge> outofthis = new List<BlockEdge>();

        // If there are two possible outputs as the
        // result of a conditional branch
        // the first block in outofthis should be
        // the result of the condition being false

        /// Update block references in edges with copy map
        /// Block references are updated using the getCopyMap() reference on the original block
        /// \param vec is the list of edges whose block references should be updated
        private static void replaceEdgeMap(List<BlockEdge> vec)
        {
            foreach (BlockEdge iter in vec) {
                iter.point = iter.point.getCopyMap();
            }
        }

        /// Add an edge coming into \b this
        /// \param b is the FlowBlock coming in
        /// \param lab is a label for the edge
        private void addInEdge(FlowBlock b, edge_flags lab)
        {
            int ourrev = b.outofthis.Count;
            int brev = intothis.Count;
            intothis.Add(new BlockEdge(b, lab, ourrev));
            b.outofthis.Add(new BlockEdge(this, lab, brev));
        }

        /// Restore the next input edge from XML
        /// Parse the next \<edge> element in the stream
        /// \param decoder is the stream decoder
        /// \param resolver is used to resolve block references
        private void decodeNextInEdge(Sla.CORE.Decoder decoder, BlockMap resolver)
        {
            BlockEdge inedge = new BlockEdge();
            intothis.Add(inedge);
            inedge.decode(decoder, resolver);
            while (inedge.point.outofthis.Count <= inedge.reverse_index) {
                inedge.point.outofthis.Add(null);
            }
            BlockEdge outedge = inedge.point.outofthis[inedge.reverse_index];
            outedge.label = 0;
            outedge.point = this;
            outedge.reverse_index = intothis.Count - 1;
        }

        /// Delete the \e in half of an edge, correcting indices
        /// \param slot is the index of the incoming edge being altered
        private void halfDeleteInEdge(int slot)
        {
            while (slot < intothis.Count - 1) {
                BlockEdge edge = intothis[slot];
                // Slide the edge entry over
                edge = intothis[slot + 1];
                // Correct the index coming the other way
                BlockEdge edger = edge.point.outofthis[edge.reverse_index];
                edger.reverse_index -= 1;
                slot += 1;
            }
            intothis.RemoveAt(intothis.Count - 1);
        }

        /// Delete the \e out half of an edge, correcting indices
        /// \param slot is the index of the outgoing edge being altered
        private void halfDeleteOutEdge(int slot)
        {
            while (slot < outofthis.size() - 1) {
                BlockEdge edge = outofthis[slot];
                // Slide the edge
                edge = outofthis[slot + 1];
                // Correct the index coming the other way
                BlockEdge edger =edge.point.intothis[edge.reverse_index];
                edger.reverse_index -= 1;
                slot += 1;
            }
            outofthis.RemoveAt(outofthis.Count - 1);
        }

        /// Remove an incoming edge
        /// \param slot is the index of the incoming edge to remove
        private void removeInEdge(int slot)
        {
            FlowBlock b = intothis[slot].point;
            int rev = intothis[slot].reverse_index;
            halfDeleteInEdge(slot);
            b.halfDeleteOutEdge(rev);
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            b.checkEdges();
#endif
        }

        /// Remove an outgoing edge
        /// \param slot is the index of the outgoing edge to remove
        private void removeOutEdge(int slot)
        {
            FlowBlock b = outofthis[slot].point;
            int rev = outofthis[slot].reverse_index;
            halfDeleteOutEdge(slot);
            b.halfDeleteInEdge(rev);
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            b.checkEdges();
#endif
        }

        /// Make an incoming edge flow from a given block
        /// The original edge, which must exist, is replaced.
        /// \param num is the index of the incoming edge
        /// \param b is the new incoming block
        private void replaceInEdge(int num, FlowBlock b)
        {
            FlowBlock oldb = intothis[num].point;
            oldb.halfDeleteOutEdge(intothis[num].reverse_index);
            intothis[num].point = b;
            intothis[num].reverse_index = b.outofthis.Count;
            b.outofthis.Add(new BlockEdge(this, intothis[num].label, num));
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            b.checkEdges();
            oldb.checkEdges();
#endif
        }

        ///< Make an outgoing edge flow to a given block
        /// The original edge, which must exist is replaced.
        /// \param num is the index of the outgoing edge
        /// \param b is the new outgoing block
        private void replaceOutEdge(int num, FlowBlock b)
        {
            FlowBlock oldb = outofthis[num].point;
            oldb.halfDeleteInEdge(outofthis[num].reverse_index);
            outofthis[num].point = b;
            outofthis[num].reverse_index = b.intothis.Count;
            b.intothis.Add(new BlockEdge(this, outofthis[num].label, num));
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            b.checkEdges();
            oldb.checkEdges();
#endif
        }

        /// Remove \b this from flow between two blocks
        /// Remove edge \b in and \b out from \b this block, but create
        /// a new edge between the in-block and the out-block, preserving
        /// position in the in/out edge lists.
        /// \param in is the index of the incoming block
        /// \param out is the index of the outgoing block
        private void replaceEdgesThru(int @in, int @out)
        {
            FlowBlock inb = intothis[@in].point;
            int inblock_outslot = intothis[@in].reverse_index;
            FlowBlock outb = outofthis[@out].point;
            int outblock_inslot = outofthis[@out].reverse_index;
            inb.outofthis[inblock_outslot].point = outb;
            inb.outofthis[inblock_outslot].reverse_index = outblock_inslot;
            outb.intothis[outblock_inslot].point = inb;
            outb.intothis[outblock_inslot].reverse_index = inblock_outslot;
            halfDeleteInEdge(@in);
            halfDeleteOutEdge(@out);
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            inb.checkEdges();
            outb.checkEdges();
#endif
        }

        /// Swap the first and second \e out edges
        private void swapEdges()
        {
#if BLOCKCONSISTENT_DEBUG
            if (outofthis.Count != 2) {
                throw new LowlevelError("Swapping edges for block that doesn't have two edges");
            }
#endif
            BlockEdge tmp = outofthis[0];
            outofthis[0] = outofthis[1];
            outofthis[1] = tmp;
            FlowBlock bl = outofthis[0].point;
            bl.intothis[outofthis[0].reverse_index].reverse_index = 0;
            bl = outofthis[1].point;
            bl.intothis[outofthis[1].reverse_index].reverse_index = 1;
            flags ^= block_flags.f_flip_path;
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
#endif
        }

        /// Apply an \e out edge label
        /// \param i is the index of the outgoing edge
        /// \param lab is the new edge label
        private void setOutEdgeFlag(int i, edge_flags lab)
        {
            FlowBlock bbout = outofthis[i].point;
            outofthis[i].label |= lab;
            bbout.intothis[outofthis[i].reverse_index].label |= lab;
        }

        /// Remove an \e out edge label
        /// \param i is the index of the outgoing edge
        /// \param lab is the edge label to remove
        private void clearOutEdgeFlag(int i, edge_flags lab)
        {
            FlowBlock bbout = outofthis[i].point;
            outofthis[i].label &= ~lab;
            bbout.intothis[outofthis[i].reverse_index].label &= ~lab;
        }

        /// Eliminate duplicate \e in edges from given block
        /// \param bl is the given block
        private void eliminateInDups(FlowBlock bl)
        {
            int indval = -1;
            int i = 0;
            while (i < intothis.Count) {
                if (intothis[i].point == bl) {
                    if (indval == -1) {
                        // The first instance of bl
                        // We keep it
                        indval = i;
                        i += 1;
                    }
                    else {
                        intothis[indval].label |= intothis[i].label;
                        int rev = intothis[i].reverse_index;
                        halfDeleteInEdge(i);
                        bl.halfDeleteOutEdge(rev);
                    }
                }
                else {
                    i += 1;
                }
            }
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            bl.checkEdges();
#endif
        }

        /// Eliminate duplicate \e out edges to given block
        /// \param bl is the given block
        private void eliminateOutDups(FlowBlock bl)
        {
            int indval = -1;
            int i = 0;
            
            while (i < outofthis.Count) {
                if (outofthis[i].point == bl) {
                    if (indval == -1) {
                        // The first instance of bl
                        // We keep it
                        indval = i;
                        i += 1;
                    }
                    else {
                        outofthis[indval].label |= outofthis[i].label;
                        int rev = outofthis[i].reverse_index;
                        halfDeleteOutEdge(i);
                        bl.halfDeleteInEdge(rev);
                    }
                }
                else {
                    i += 1;
                }
            }
#if BLOCKCONSISTENT_DEBUG
            checkEdges();
            bl.checkEdges();
#endif
        }

        /// \brief Find blocks that are at the end of multiple edges
        /// \param ref is the list of BlockEdges to search
        /// \param duplist will contain the list of blocks with duplicate edges
        private static void findDups(List<BlockEdge> @ref, List<FlowBlock> duplist)
        {
            foreach (BlockEdge iter in @ref) {
                if ((iter.point.flags & block_flags.f_mark2) != 0) {
                    // Already marked as a duplicate
                    continue;
                }
                if ((iter.point.flags & block_flags.f_mark) != 0) {
                    // We have a duplicate
                    duplist.Add(iter.point);
                    iter.point.flags |= block_flags.f_mark2;
                }
                else {
                    iter.point.flags |= block_flags.f_mark;
                }
            }
            foreach (BlockEdge iter in @ref) {
                // Erase our marks
                iter.point.flags &= ~(block_flags.f_mark | block_flags.f_mark2);
            }
        }

        /// Eliminate duplicate edges
        private void dedup()
        {
            List<FlowBlock> duplist = new List<FlowBlock>();

            findDups(intothis, duplist);
            foreach (FlowBlock iter in duplist) {
                eliminateInDups(iter);
            }

            duplist.Clear();
            findDups(outofthis, duplist);
            foreach (FlowBlock iter in duplist) {
                eliminateOutDups(iter);
            }
        }

        /// Update references to other blocks using getCopyMap()
        /// Run through incoming and outgoing edges and replace FlowBlock references with
        /// the FlowBlock accessed via the getCopyMap() method.
        private void replaceUsingMap()
        {
            replaceEdgeMap(intothis);
            replaceEdgeMap(outofthis);
            if (null != immed_dom) {
                immed_dom = immed_dom.getCopyMap();
            }
        }

#if BLOCKCONSISTENT_DEBUG
        /// Check the consistency of edge references
        /// Make sure block references in the BlockEdge objects owned
        /// by \b this block, and any other block at the other end of these edges,
        /// are consistent.
        private void checkEdges()
        {
        for(int i=0;i<intothis.size();++i) {
            BlockEdge edge = new BlockEdge(intothis[i] );
            int rev = edge.reverse_index;
            FlowBlock bl = edge.point;
            if (bl.outofthis.Count <= rev) {
                throw new LowlevelError("Not enough outofthis blocks");
            }
            BlockEdge edger = new BlockEdge(bl.outofthis[rev] );
            if (edger.point != this) {
                throw new LowlevelError("Intothis edge mismatch");
            }
            if (edger.reverse_index != i)
                throw new LowlevelError("Intothis index mismatch");
            }
            for(int i=0;i < outofthis.Count;++i) {
                BlockEdge edge = new BlockEdge(outofthis[i]);
                int rev = edge.reverse_index;
                FlowBlock bl = edge.point;
                if (bl.intothis.Count <= rev) {
                    throw new LowlevelError("Not enough intothis blocks");
                }
                BlockEdge edger = new BlockEdge(bl.intothis[rev]);
                if (edger.point != this) {
                    throw new LowlevelError("Outofthis edge mismatch");
                }
                if (edger.reverse_index != i) {
                    throw new LowlevelError("Outofthis index mismatch");
                }
            }
        }
#endif

        /// Set a boolean property
        internal void setFlag(block_flags fl)
        {
            flags |= fl;
        }

        /// Clear a boolean property
        internal void clearFlag(FlowBlock.block_flags fl)
        {
            flags &= ~fl;
        }

        /// Construct a block with no edges
        public FlowBlock()
        {
            flags = 0;
            index = 0;
            visitcount = 0;
            parent = null;
            immed_dom = null;
        }

        /// Destructor
        ~FlowBlock()
        {
        }

        /// Get the index assigned to \b this block
        public int getIndex() => index;

        /// Get the parent FlowBlock of \b this
        public FlowBlock getParent() => parent;

        /// Get the immediate dominator FlowBlock
        public FlowBlock getImmedDom() => immed_dom;

        /// Get the mapped FlowBlock
        public FlowBlock getCopyMap() => copymap;

        /// Get the block_flags properties
        public block_flags getFlags() => flags;

        /// Get the starting address of code in \b this FlowBlock
        public virtual Address getStart() => new Address();

        /// Get the ending address of code in \b this FlowBlock
        public virtual Address getStop() => new Address();

        /// Get the FlowBlock type of \b this
        public virtual block_type getType() => block_type.t_plain;

        /// Get the i-th component block
        public virtual FlowBlock? subBlock(int i)
        {
            return null;
        }

        /// Mark target blocks of any unstructured edges
        public virtual void markUnstructured()
        {
        }

        /// Let hierarchical blocks steal labels of their (first) components
        /// \param bump if \b true, mark that labels for this block are printed by somebody higher in hierarchy
        public virtual void markLabelBumpUp(bool bump)
        {
            if (bump) {
                flags |= block_flags.f_label_bumpup;
            }
        }

        /// Mark unstructured edges that should be \e breaks
        public virtual void scopeBreak(int curexit, int curloopexit)
        {
        }

        /// Print a simple description of \b this to stream
        /// Only print a header for \b this single block
        /// \param s is the output stream
        public virtual void printHeader(TextWriter s)
        {
            s.Write("{index}");
            if (!getStart().isInvalid() && !getStop().isInvalid()) {
                s.Write($" {getStart()}-{getStop()}");
            }
        }

        /// Print tree structure of any blocks owned by \b this
        /// Recursively print out the hierarchical structure of \b this FlowBlock.
        /// \param s is the output stream
        /// \param level is the current level of indentation
        public virtual void printTree(TextWriter s, int level)
        {
            for (int i = 0; i < level; ++i)
                s.Write("  ");
            printHeader(s);
            s.WriteLine();
        }

        /// Print raw instructions contained in \b this FlowBlock
        public virtual void printRaw(TextWriter s)
        {
        }

        /// Emit the instructions in \b this FlowBlock as structured code
        /// This is the main entry point, at the control-flow level, for printing structured code.
        /// \param lng is the PrintLanguage that provides details of the high-level language being printed
        public virtual void emit(PrintLanguage lng)
        {
        }
        
        /// Get the FlowBlock to which \b this block exits
        public virtual FlowBlock? getExitLeaf()
        {
            return null;
        }

        /// Get the last PcodeOp executed by \b this FlowBlock
        public virtual PcodeOp lastOp()
        {
            return null;
        }

        /// Flip the condition computed by \b this
        /// Flip the order of outgoing edges (at least).
        /// This should also affect the original op causing the condition.
        /// Note: we don't have to flip at all levels of the hierarchy
        /// only at the top and at the bottom
        /// \param toporbottom is \b true if \b this is the top outermost block of the hierarchy getting negated
        /// \return \b true if a change was made to data-flow
        public virtual bool negateCondition(bool toporbottom)
        {
            if (!toporbottom) {
                // No change was made to data-flow
                return false;
            }
            swapEdges();
            return false;
        }

        /// Rearrange \b this hierarchy to simplify boolean expressions
        /// For the instructions in this block, decide if the control-flow structure
        /// can be rearranged so that boolean expressions come out more naturally.
        /// \param data is the function to analyze
        /// \return \b true if a change was made
        public virtual bool preferComplement(Funcdata data)
        {
            return false;
        }

        /// Get the leaf splitting block
        /// If \b this block ends with a conditional branch, return the
        /// deepest component block that performs the split.  This component needs
        /// to be able to perform flipInPlaceTest() and flipInPlaceExecute()
        /// \return the component FlowBlock or NULL if this doesn't end in a conditional branch
        public virtual FlowBlock? getSplitPoint()
        {
            return null;
        }

        /// \brief Test normalizing the conditional branch in \b this
        ///
        /// Find the set of PcodeOp objects that need to be adjusted to flip
        /// the condition \b this FlowBlock calculates.
        ///
        /// Return:
        ///   - 0 if the flip would normalize the condition
        ///   - 1 if the flip doesn't affect normalization of the condition
        ///   - 2 if the flip produces an unnormalized condition
        /// \param fliplist will contain the PcodeOps that need to be adjusted
        /// \return 0 if the condition will be normalized, 1 or 2 otherwise
        public virtual int flipInPlaceTest(List<PcodeOp> fliplist)
        {
            // By default a block will not normalize
            return 2;
        }

        /// \brief Perform the flip to normalize conditional branch executed by \b this block
        /// This reverses the outgoing edge order in the right basic blocks, but does not
        /// modify the instructions directly.
        public virtual void flipInPlaceExecute()
        {
        }

        /// Is \b this too complex to be a condition (BlockCondition)
        public virtual bool isComplex()
        {
            return true;
        }

        /// \brief Get the leaf FlowBlock that will execute after the given FlowBlock
        ///
        /// Within the hierarchy of \b this FlowBlock, assume the given FlowBlock
        /// will fall-thru in its execution at some point. Return the first
        /// leaf block (BlockBasic or BlockCopy) that will execute after the given
        /// FlowBlock completes, assuming this is a unique block.
        /// \param bl is the given FlowBlock
        /// \return the next FlowBlock to execute or NULL
        public virtual FlowBlock? nextFlowAfter(FlowBlock bl)
        {
            return null;
        }

        /// Do any structure driven final transforms
        public virtual void finalTransform(Funcdata data)
        {
        }

        /// Make any final configurations necessary to print the block
        public virtual void finalizePrinting(Funcdata data)
        {
        }

        /// Encode basic information as attributes
        /// \param encoder is the stream encoder
        public virtual void encodeHeader(Sla.CORE.Encoder encoder)
        {
            encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, index);
        }

        /// Decode basic information from element attributes
        /// \param decoder is the stream decoder to pull attributes from
        public virtual void decodeHeader(Sla.CORE.Decoder decoder)
        {
            index = (int)decoder.readSignedInteger(AttributeId.ATTRIB_INDEX);
        }

        ///< Encode detail about components to a stream
        public virtual void encodeBody(Sla.CORE.Encoder encoder)
        {
        }

        /// \brief Restore details about \b this FlowBlock from an element stream
        /// \param decoder is the stream decoder
        public virtual void decodeBody(Sla.CORE.Decoder decoder)
        {
        }

        /// Encode edge information to a stream
        /// Write \<edge> element to a stream
        /// \param encoder is the stream encoder
        public void encodeEdges(Sla.CORE.Encoder encoder)
        {
            for (int i = 0; i < intothis.size(); ++i) {
                intothis[i].encode(encoder);
            }
        }

        /// \brief Restore edges from an encoded stream
        ///
        /// \param decoder is the stream decoder
        /// \param resolver is used to recover FlowBlock cross-references
        public void decodeEdges(Sla.CORE.Decoder decoder, BlockMap resolver)
        {
            while(true)
            {
                uint subId = decoder.peekElement();
                if (subId != ElementId.ELEM_EDGE)
                    break;
                decodeNextInEdge(decoder, resolver);
            }
        }

        /// Encode \b this to a stream
        /// Encode \b this and all its sub-components as a \<block> element.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_BLOCK);
            encodeHeader(encoder);
            encodeBody(encoder);
            encodeEdges(encoder);
            encoder.closeElement(ElementId.ELEM_BLOCK);
        }

        /// Decode \b this from a stream
        /// Recover \b this and all it sub-components from a \<block> element.
        ///
        /// This will construct all the sub-components using \b resolver as a factory.
        /// \param decoder is the stream decoder
        /// \param resolver acts as a factory and resolves cross-references
        public void decode(Sla.CORE.Decoder decoder, BlockMap resolver)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_BLOCK);
            decodeHeader(decoder);
            decodeBody(decoder);
            decodeEdges(decoder, resolver);
            decoder.closeElement(elemId);
        }

        /// Return next block to be executed in flow
        /// If there are two branches, pick the fall-thru branch
        /// \return the next block in flow, or NULL otherwise
        public FlowBlock? nextInFlow()
        {
            PcodeOp? op;

            if (sizeOut() == 1) return getOut(0);
            if (sizeOut() == 2) {
                op = lastOp();
                if (op == (PcodeOp)null) return (FlowBlock)null;
                if (op.code() != OpCode.CPUI_CBRANCH) return (FlowBlock)null;
                return op.isFallthruTrue() ? getOut(1) : getOut(0);
            }
            return (FlowBlock)null;
        }

        /// Set the number of times this block has been visited
        public void setVisitCount(int i)
        {
            visitcount = i;
        }

        /// Get the count of visits
        public int getVisitCount()
        {
            return visitcount;
        }

        /// Mark a \e goto branch
        /// This is the main entry point for marking a branch
        /// from one block to another as unstructured.
        /// \param i is the index of the outgoing edge to mark
        public void setGotoBranch(int i)
        {
            if ((i >= 0) && (i < outofthis.Count)) {
                setOutEdgeFlag(i, edge_flags.f_goto_edge);
            }
            else {
                throw new LowlevelError("Could not find block edge to mark unstructured");
            }
            // Mark that there is a goto out of this block
            flags |= block_flags.f_interior_gotoout;
            outofthis[i].point.flags |= block_flags.f_interior_gotoin;
        }

        /// Mark an edge as the switch default
        /// The switch can have exactly 1 default edge, so we make sure other edges are not marked.
        /// \param pos is the index of the \e out edge that should be the default
        public void setDefaultSwitch(int pos)
        {
            for (int i = 0; i < outofthis.Count; ++i) {
                if (isDefaultBranch(i)) {
                    // Clear any previous flag
                    clearOutEdgeFlag(i, edge_flags.f_defaultswitch_edge);
                }
            }
            setOutEdgeFlag(pos, edge_flags.f_defaultswitch_edge);
        }

        /// Return \b true if \b this block has been marked
        public bool isMark()
        {
            return ((flags & block_flags.f_mark) != 0);
        }

        /// Mark \b this block
        public void setMark()
        {
            flags |= block_flags.f_mark;
        }

        /// Clear any mark on \b this block
        public void clearMark()
        {
            flags &= ~block_flags.f_mark;
        }

        /// Label \b this as a \e do \e nothing loop
        public void setDonothingLoop()
        {
            flags |= block_flags.f_donothing_loop;
        }

        /// Label \b this as dead
        public void setDead()
        {
            flags |= block_flags.f_dead;
        }

        /// Return \b true if \b this uses a different label
        public bool hasSpecialLabel()
        {
            return ((flags & (block_flags.f_joined_block | block_flags.f_duplicate_block)) != 0);
        }

        /// Return \b true if \b this is a \e joined basic block
        public bool isJoined()
        {
            return ((flags & block_flags.f_joined_block) != 0);
        }

        /// Return \b true if \b this is a \e duplicated block
        public bool isDuplicated()
        {
            return ((flags & block_flags.f_duplicate_block) != 0);
        }

        /// Label the edge exiting \b this as a loop
        public void setLoopExit(int i)
        {
            setOutEdgeFlag(i, edge_flags.f_loop_exit_edge);
        }

        /// Clear the loop exit edge
        public void clearLoopExit(int i)
        {
            clearOutEdgeFlag(i, edge_flags.f_loop_exit_edge);
        }

        /// Label the \e back edge of a loop
        public void setBackEdge(int i)
        {
            setOutEdgeFlag(i, edge_flags.f_back_edge);
        }

        /// Have out edges been flipped
        public bool getFlipPath()
        {
            return ((flags & block_flags.f_flip_path)!= 0);
        }

        /// Return \b true if non-fallthru jump flows into \b this
        /// \b return \b true if block is the target of a jump
        public bool isJumpTarget()
        {
            for (int i = 0; i < intothis.Count; ++i) {
                if (intothis[i].point.index != index - 1) {
                    return true;
                }
            }
            return false;
        }

        /// Get the \b false output FlowBlock
        public FlowBlock getFalseOut()
        {
            return outofthis[0].point;
        }

        /// Get the \b true output FlowBlock
        public FlowBlock getTrueOut()
        {
            return outofthis[1].point;
        }

        /// Get the i-th output FlowBlock
        public FlowBlock getOut(int i)
        {
            return outofthis[i].point;
        }

        /// Get the input index of the i-th output FlowBlock
        public int getOutRevIndex(int i)
        {
            return outofthis[i].reverse_index;
        }

        /// Get the i-th input FlowBlock
        public FlowBlock getIn(int i)
        {
            return intothis[i].point;
        }

        /// Get the output index of the i-th input FlowBlock
        public int getInRevIndex(int i)
        {
            return intothis[i].reverse_index;
        }

        /// Get the first leaf FlowBlock
        /// Keep descending tree hierarchy, taking the front block,
        /// until we get to the bottom copy block
        /// \return the first leaf FlowBlock to execute
        public FlowBlock? getFrontLeaf()
        {
            FlowBlock? bl = this;
            while (bl.getType() != block_type.t_copy) {
                bl = bl.subBlock(0);
                if (null == bl) {
                    return null;
                }
            }
            return bl;
        }


        /// Get the depth of the given component FlowBlock
        /// How many getParent() calls from the leaf to \b this
        /// \param leaf is the component FlowBlock
        /// \return the depth count
        public int calcDepth(FlowBlock leaf)
        {
            int depth = 0;
            while (leaf != this) {
                if (null == leaf) {
                    return -1;
                }
                leaf = leaf.getParent();
                depth += 1;
            }
            return depth;
        }

        /// Does \b this block dominate the given block
        /// Return \b true if \b this block \e dominates the given block (or is equal to it).
        /// This assumes that block indices have been set with a reverse post order so that having a
        /// smaller index is a necessary condition for dominance.
        /// \param subBlock is the given block to test against \b this for dominance
        /// \return \b true if \b this dominates
        public bool dominates(FlowBlock subBlock)
        {
            while (null != subBlock && index <= subBlock.index) {
                if (subBlock == this) {
                    return true;
                }
                subBlock = subBlock.getImmedDom();
            }
            return false;
        }

        /// \brief Check if the condition from the given block holds for \b this block
        /// We assume the given block has 2 out-edges and that \b this block is immediately reached by
        /// one of these two edges. Some condition holds when traversing the out-edge to \b this, and the complement
        /// of the condition holds for traversing the other out-edge. We verify that the condition holds for
        /// this entire block.  More specifically, we check that that there is no path to \b this through the
        /// sibling edge, where the complement of the condition holds (unless we loop back through the conditional block).
        /// \param cond is the conditional block with 2 out-edges
        /// \return \b true if the condition holds for this block
        public bool restrictedByConditional(FlowBlock cond)
        {
            if (sizeIn() == 1){
                // Its impossible for any path to come through sibling to this
                return true;
            }
            if (getImmedDom() != cond) {
                // This is not dominated by conditional  block at all
                return false;
            }
            for (int i = 0; i < sizeIn(); ++i) {
                FlowBlock inBlock = getIn(i);
                if (inBlock == cond) {
                    // The unique edge from cond to this
                    continue;
                }
                while (inBlock != this) {
                    if (inBlock == cond) {
                        // Must have come through sibling
                        return false;
                    }
                    inBlock = inBlock.getImmedDom();
                }
            }
            return true;
        }

        /// Get the number of out edges
        public int sizeOut()
        {
            return outofthis.size();
        }

        /// Get the number of in edges
        public int sizeIn()
        {
            return intothis.size();
        }

        /// Is there a looping edge coming into \b this block
        /// \return \b true if \b this is the top of a loop
        public bool hasLoopIn()
        {
            for (int i = 0; i < intothis.Count; ++i) {
                if ((intothis[i].label & edge_flags.f_loop_edge) != 0) {
                    return true;
                }
            }
            return false;
        }

        /// Is there a looping edge going out of \b this block
        /// \return \b true if \b this is the bottom of a loop
        public bool hasLoopOut()
        {
            for (int i = 0; i < outofthis.Count; ++i) {
                if ((outofthis[i].label & edge_flags.f_loop_edge) != 0) {
                    return true;
                }
            }
            return false;
        }

        /// Is the i-th incoming edge a \e loop edge
        public bool isLoopIn(int i)
        {
            return ((intothis[i].label & edge_flags.f_loop_edge)!= 0);
        }

        /// Is the i-th outgoing edge a \e loop edge
        public bool isLoopOut(int i)
        {
            return ((outofthis[i].label & edge_flags.f_loop_edge)!= 0);
        }

        /// Get the incoming edge index for the given FlowBlock
        /// Search through incoming blocks in edge order for the given block.
        /// \param bl is the given FlowBlock
        /// \return the matching edge index or -1 if \b bl doesn't flow into \b this
        public int getInIndex(FlowBlock bl)
        {
            for (int blocknum = 0; blocknum < intothis.Count; ++blocknum) {
                if (intothis[blocknum].point == bl) {
                    return blocknum;
                }
            }
            // That block not found
            return -1;
        }

        /// Get the outgoing edge index for the given FlowBlock
        /// Search through outgoing blocks in edge order for the given block.
        /// \param bl is the given FlowBlock
        /// \return the matching edge index or -1 if \b bl doesn't flow out of \b this
        public int getOutIndex(FlowBlock bl)
        {
            for (int blocknum = 0; blocknum < outofthis.Count; ++blocknum) {
                if (outofthis[blocknum].point == bl) {
                    return blocknum;
                }
            }
            return -1;
        }

        /// Is the i-th out edge the switch default edge
        public bool isDefaultBranch(int i) => ((outofthis[i].label & edge_flags.f_defaultswitch_edge) != 0);

        /// Are labels for \b this printed by the parent
        public bool isLabelBumpUp() => ((flags & block_flags.f_label_bumpup) != 0);

        /// Is \b this the target of an unstructured goto
        public bool isUnstructuredTarget() => ((flags & block_flags.f_unstructured_targ) != 0);

        /// Is there an unstructured goto to \b this block's interior
        public bool isInteriorGotoTarget() => ((flags & block_flags.f_interior_gotoin) != 0);

        /// Is there an unstructured goto out of \b this block's interior
        public bool hasInteriorGoto() => ((flags & block_flags.f_interior_gotoout)!= 0);

        /// Is the entry point of the function
        public bool isEntryPoint() => ((flags & block_flags.f_entry_point) != 0);

        /// Is \b this a switch block
        public bool isSwitchOut() => ((flags & block_flags.f_switch_out) != 0);

        /// Is \b this a \e do \e nothing block
        public bool isDonothingLoop() => ((flags & block_flags.f_donothing_loop) != 0);

        /// Is \b this block dead
        public bool isDead() => ((flags & block_flags.f_dead)!= 0);

        /// Is the i-th incoming edge part of the spanning tree
        public bool isTreeEdgeIn(int i)
        {
            return ((intothis[i].label & edge_flags.f_tree_edge)!= 0);
        }

        /// Is the i-th incoming edge a \e back edge
        public bool isBackEdgeIn(int i)
        {
            return ((intothis[i].label & edge_flags.f_back_edge)!= 0);
        }

        /// Is the i-th outgoing edge a \e back edge
        public bool isBackEdgeOut(int i)
        {
            return ((outofthis[i].label & edge_flags.f_back_edge)!= 0);
        }

        /// Is the i-th outgoing edge an irreducible edge
        public bool isIrreducibleOut(int i)
        {
            return ((outofthis[i].label & edge_flags.f_irreducible)!= 0);
        }

        /// Is the i-th incoming edge an irreducible edge
        public bool isIrreducibleIn(int i)
        {
            return ((intothis[i].label & edge_flags.f_irreducible)!= 0);
        }

        /// \brief Can \b this and the i-th output be merged into a BlockIf or BlockList
        public bool isDecisionOut(int i)
        {
            return ((outofthis[i].label & (edge_flags.f_irreducible | edge_flags.f_back_edge | edge_flags.f_goto_edge))== 0);
        }

        /// \brief Can \b this and the i-th input be merged into a BlockIf or BlockList
        public bool isDecisionIn(int i)
        {
            return ((intothis[i].label & (edge_flags.f_irreducible | edge_flags.f_back_edge | edge_flags.f_goto_edge))== 0);
        }

        /// \brief Is the i-th outgoing edge part of the DAG sub-graph
        public bool isLoopDAGOut(int i)
        {
            return ((outofthis[i].label & (edge_flags.f_irreducible | edge_flags.f_back_edge | edge_flags.f_loop_exit_edge | edge_flags.f_goto_edge))== 0);
        }

        /// \brief Is the i-th incoming edge part of the DAG sub-graph
        public bool isLoopDAGIn(int i)
        {
            return ((intothis[i].label & (edge_flags.f_irreducible | edge_flags.f_back_edge | edge_flags.f_loop_exit_edge | edge_flags.f_goto_edge))== 0);
        }

        /// Is the i-th incoming edge unstructured
        public bool isGotoIn(int i)
        {
            return ((intothis[i].label & (edge_flags.f_irreducible | edge_flags.f_goto_edge))!= 0);
        }

        /// Is the i-th outgoing edge unstructured
        public bool isGotoOut(int i)
        {
            return ((outofthis[i].label & (edge_flags.f_irreducible | edge_flags.f_goto_edge))!= 0);
        }

        /// Get the JumpTable associated \b this block
        /// If \b this FlowBlock was ends with a computed jump, retrieve
        /// the associated JumpTable object
        /// \return the JumpTable object or NULL
        public JumpTable? getJumptable()
        {
            JumpTable? jt = null;
            if (!isSwitchOut()) {
                return jt;
            }
            PcodeOp? indop = lastOp();
            if (null != indop) {
                jt = indop.getParent().getFuncdata().findJumpTable(indop);
            }
            return jt;
        }

        /// Get the block_type associated with a name string
        /// Given a string describing a FlowBlock type, return the block_type.
        /// This is currently only used by the decode() process.
        /// TODO: Fill in the remaining names and types
        /// \param nm is the name string
        /// \return the corresponding block_type
        public static block_type nameToType(string name)
        {
            switch (name) {
                case "graph":
                    return block_type.t_graph;
                case "copy":
                    return block_type.t_copy;
                default:
                    return block_type.t_plain;
            }
        }

        /// Get the name string associated with a block_type
        /// For use in serializng FlowBlocks to XML.
        /// \param bt is the block_type
        /// \return the corresponding name string
        public static string typeToName(block_type bt)
        {
            switch (bt) {
                case block_type.t_plain:
                    return "plain";
                case block_type.t_basic:
                    return "basic";
                case block_type.t_graph:
                    return "graph";
                case block_type.t_copy:
                    return "copy";
                case block_type.t_goto:
                    return "goto";
                case block_type.t_multigoto:
                    return "multigoto";
                case block_type.t_ls:
                    return "list";
                case block_type.t_condition:
                    return "condition";
                case block_type.t_if:
                    return "properif";
                case block_type.t_whiledo:
                    return "whiledo";
                case block_type.t_dowhile:
                    return "dowhile";
                case block_type.t_switch:
                    return "switch";
                case block_type.t_infloop:
                    return "infloop";
                default:
                    return string.Empty;
            }
        }

        /// Compare FlowBlock by index
        public static bool compareBlockIndex(FlowBlock bl1, FlowBlock bl2)
        {
            return (bl1.getIndex() < bl2.getIndex());
        }

        /// Final FlowBlock comparison
        /// Comparator for ordering the final 0-exit blocks
        /// \param bl1 is the first FlowBlock to compare
        /// \param bl2 is the second FlowBlock
        /// \return true if the first comes before the second
        public static bool compareFinalOrder(FlowBlock bl1, FlowBlock bl2)
        {
            if (bl1.getIndex() == 0) {
                // Make sure the entry point comes first
                return true;
            }
            if (bl2.getIndex() == 0) {
                return false;
            }
            PcodeOp? op1 = bl1.lastOp();
            PcodeOp? op2 = bl2.lastOp();

            if (null != op1) {
                // Make sure return blocks come last
                if (null != op2) {
                    if ((op1.code() == OpCode.CPUI_RETURN) && (op2.code() != OpCode.CPUI_RETURN)) {
                        return false;
                    }
                    else if ((op1.code() != OpCode.CPUI_RETURN) && (op2.code() == OpCode.CPUI_RETURN)) {
                        return true;
                    }
                }
                if (op1.code() == OpCode.CPUI_RETURN) {
                    return false;
                }
            }
            else if (null != op2) {
                if (op2.code() == OpCode.CPUI_RETURN) {
                    return true;
                }
            }
            // Otherwise use index
            return (bl1.getIndex() < bl2.getIndex());
        }

        /// Find the common dominator of two FlowBlocks
        /// Within the dominator tree, find the earliest common ancestor of two FlowBlocks
        /// \param bl1 is the first FlowBlock
        /// \param bl2 is the second
        /// \return the common ancestor which dominates both
        public static FlowBlock? findCommonBlock(FlowBlock bl1, FlowBlock bl2)
        {
            FlowBlock b1 = bl1;
            FlowBlock b2 = bl2;
            FlowBlock? common = null;

            while (true) {
                if (null == b2) {
                    while (null != b1) {
                        if (b1.isMark()) {
                            common = b1;
                            break;
                        }
                        b1 = b1.getImmedDom();
                    }
                    break;
                }
                if (null == b1) {
                    while (null != b2) {
                        if (b2.isMark()) {
                            common = b2;
                            break;
                        }
                        b2 = b2.getImmedDom();
                    }
                    break;
                }
                if (b1.isMark()) {
                    common = b1;
                    break;
                }
                b1.setMark();
                if (b2.isMark()) {
                    common = b2;
                    break;
                }
                b2.setMark();
                b1 = b1.getImmedDom();
                b2 = b2.getImmedDom();
            }
            // Clear our marks
            while (null != bl1) {
                if (!bl1.isMark()) {
                    break;
                }
                bl1.clearMark();
                bl1 = bl1.getImmedDom();
            }
            while (null != bl2) {
                if (!bl2.isMark()) {
                    break;
                }
                bl2.clearMark();
                bl2 = bl2.getImmedDom();
            }
            return common;
        }

        /// Find common dominator of multiple FlowBlocks
        /// Find the most immediate dominating FlowBlock of all blocks in the given set.
        /// The container must not be empty.
        /// \param blockSet is the given set of blocks
        /// \return the most immediate dominating FlowBlock
        public static FlowBlock findCommonBlock(List<FlowBlock> blockSet)
        {
            List<FlowBlock> markedSet = new List<FlowBlock>();
            FlowBlock bl;
            FlowBlock res = blockSet[0];
            int bestIndex = res.getIndex();
            bl = res;
            do {
                bl.setMark();
                markedSet.Add(bl);
                bl = bl.getImmedDom();
            } while (null != bl);
            for (int i = 1; i < blockSet.Count; ++i) {
                if (bestIndex == 0) {
                    break;
                }
                bl = blockSet[i];
                while (!bl.isMark()) {
                    bl.setMark();
                    markedSet.Add(bl);
                    bl = bl.getImmedDom();
                }
                if (bl.getIndex() < bestIndex) {
                    // If first meeting with old paths is higher than ever before
                    // we have a new best
                    res = bl;
                    bestIndex = res.getIndex();
                }
            }
            for (int i = 0; i < markedSet.Count; ++i) {
                markedSet[i].clearMark();
            }
            return res;
        }
    }
}
