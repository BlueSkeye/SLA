using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A basic block for p-code operations.
    ///
    /// A \b basic \b block is a maximal sequence of p-code operations (PcodeOp) that,
    /// within the context of a function, always execute starting with the first
    /// operation in sequence through in order to the last operation.  Any decision points in the
    /// control flow of a function manifest as branching operations (BRANCH, CBRANCH, BRANCHIND)
    /// that necessarily occur as the last operation in a basic block.
    ///
    /// Every Funcdata object implements the control-flow graph of the underlying function using
    /// BlockBasic objects as the underlying nodes of the graph.  The decompiler structures code
    /// by making a copy of this graph and then overlaying a hierarchy of structured nodes on top of it.
    ///
    /// The block also keeps track of the original range of addresses of instructions constituting the block.
    /// As decompiler transformations progress, the set of addresses associated with the current set of
    /// PcodeOps my migrate away from this original range.
    internal class BlockBasic : FlowBlock
    {
        // Only uses private functions
        // friend class Funcdata;
        /// The sequence of p-code operations
        internal LinkedList<PcodeOp> op = new LinkedList<PcodeOp>();
        /// The function of which this block is a part
        private Funcdata data;
        /// Original range of addresses covered by this basic block
        private RangeList cover;

        /// Original range of addresses covered by this basic block
        /// The operation is inserted \e before the PcodeOp pointed at by the iterator.
        /// This method also assigns the ordering index for the PcodeOp, getSeqNum().getOrder()
        /// \param insertBefore points at the PcodeOp to insert before or null to add at end.
        /// \param inst is the PcodeOp to insert
        internal void insert(LinkedListNode<PcodeOp>? insertBefore, PcodeOp inst)
        {
            uint ordbefore;
            uint ordafter;
            // IEnumerator<PcodeOp> newiter;

            // Make sure the op node is instanciated.
            if (null == inst._basicBlockNode) {
                inst._basicBlockNode = new LinkedListNode<PcodeOp>(inst);
            }
            else if (null != inst._basicBlockNode.List) {
                throw new InvalidOperationException();
            }
            // newiter = op.Insert(insertBefore, inst);
            if (null == insertBefore) {
                op.AddLast(inst._basicBlockNode);
            }
            else if (null == insertBefore.Previous) {
                op.AddFirst(inst._basicBlockNode);
            }
            else {
                op.AddBefore(inst._basicBlockNode, inst._basicBlockNode);
            }
            inst.setParent(this);
            // inst.setBasicIter(newiter);
            if (null == inst._basicBlockNode.Previous) {
                // This is minimum possible order val
                ordbefore = 2;
            }
            else {
                // --newiter;
                ordbefore = inst._basicBlockNode.Previous.Value.getSeqNum().getOrder();
            }
            if (null == inst._basicBlockNode.Previous) {
                ordafter = ordbefore + 0x1000000;
                if (ordafter <= ordbefore)
                    ordafter = uint.MaxValue;
            }
            else {
                ordafter = inst._basicBlockNode.Value.getSeqNum().getOrder();
            }
            if (ordafter - ordbefore <= 1) {
                setOrder();
            }
            else {
                // Beware overflow
                inst.setOrder(ordafter / 2 + ordbefore / 2);
            }
            if (inst.isBranch()) {
                if (inst.code() == OpCode.CPUI_BRANCHIND)
                    setFlag(block_flags.f_switch_out);
            }
        }

        /// Set the initial address range of the block
        /// In terms of machine instructions, a basic block always covers a range of addresses,
        /// from its first instruction to its last. This method establishes that range.
        /// \param beg is the address of the first instruction in the block
        /// \param end is the address of the last instruction in the block
        internal void setInitialRange(Address beg, Address end)
        {
            cover.clear();
            // TODO: We could check that -beg- and -end- are in the same address space
            cover.insertRange(beg.getSpace(), beg.getOffset(), end.getOffset());
        }

        /// Copy address ranges from another basic block
        internal void copyRange(BlockBasic bb)
        {
            cover = bb.cover;
        }

        /// Merge address ranges from another basic block
        internal void mergeRange(BlockBasic bb)
        {
            cover.merge(bb.cover);
        }

        /// Reset the \b SeqNum::order field for all PcodeOp objects in this block
        /// The SeqNum::order field for each PcodeOp must mirror the ordering of that PcodeOp within
        /// \b this block.  Insertions are usually handled by calculating an appropriate SeqNum::order field
        /// for the new PcodeOp, but sometime there isn't enough room between existing ops.  This method is
        /// then called to recalculate the SeqNum::order field for all PcodeOp objects in \b this block,
        /// reestablishing space between the field values.
        internal void setOrder()
        {
            uint step = uint.MaxValue;
            step = (uint)((step / op.Count) - 1);
            uint count = 0;
            foreach (PcodeOp iter in op) {
                count += step;
                iter.setOrder(count);
            }
        }

        /// Remove PcodeOp from \b this basic block
        /// \param inst is the PcodeOp to remove, which \e must be in the block
        internal void removeOp(PcodeOp inst)
        {
            if (null == inst._basicBlockNode) throw new InvalidOperationException();
            op.Remove(inst._basicBlockNode);
            inst.setParent(null);
        }

        /// Construct given the underlying function
        public BlockBasic(Funcdata fd)
        {
            data = fd;
        }

        /// Return the underlying Funcdata object
        public Funcdata getFuncdata() => data;

        /// Determine if the given address is contained in the original range
        public bool contains(Address addr)
        {
            return cover.inRange(addr, 1);
        }

        /// Get the address of the (original) first operation to execute
        /// This relies slightly on \e normal semantics: when instructions \e fall-thru during execution,
        /// the associated address increases.
        /// \return the address of the original entry point instruction for \b this block
        public Address getEntryAddr()
        {
            if (cover.numRanges() == 1) {
                // If block consists of 1 range
                // return the start of range
                return cover.getFirstRange().getFirstAddr();
            }
            if (op.empty()) {
                return new Address();
            }
            // Find range of first op
            Address addr = op.First.Value.getAddr();
            Sla.CORE.Range? range = cover.getRange(addr.getSpace(), addr.getOffset());
            return (range == null) 
                ? op.First.Value.getAddr()
                : range.getFirstAddr();
        }

        public override Address getStart()
        {
            Sla.CORE.Range? range = cover.getFirstRange();
            return (null == range) ? new Address() : range.getFirstAddr();
        }

        public override Address getStop()
        {
            Sla.CORE.Range? range = cover.getLastRange();
            return (null == range) ? new Address() : range.getLastAddr();
        }

        public override block_type getType() => block_type.t_basic;

        public override FlowBlock? subBlock(int i) => null;

        public override void encodeBody(Encoder encoder)
        {
            cover.encode(encoder);
        }

        public override void decodeBody(Decoder decoder)
        {
            cover.decode(decoder);
        }

        public override void printHeader(TextWriter s)
        {
            s.Write("Basic Block ");
            base.printHeader(s);
        }

        public override void printRaw(TextWriter s)
        {
            printHeader(s);
            s.WriteLine();
            foreach (PcodeOp iter in op) {
                PcodeOp inst = iter;
                s.Write("{inst.getSeqNum()}:\t");
                inst.printRaw(s);
                s.WriteLine();
            }
        }

        public override void emit(PrintLanguage lng)
        {
            lng.emitBlockBasic(this);
        }
  
        public override FlowBlock getExitLeaf() => this;

        public override PcodeOp? lastOp() => (0 == op.Count) ? null : op.Last.Value;

        public override bool negateCondition(bool toporbottom)
        {
            PcodeOp lastop = op.Last.Value;
            // Flip the meaning of condition
            lastop.flipFlag(PcodeOp.Flags.boolean_flip);
            // Flip whether fall-thru block is true/false
            lastop.flipFlag(PcodeOp.Flags.fallthru_true);
            // Flip the order of outgoing edges
            base.negateCondition(true);
            // Return -true- to indicate a change was made to data-flow
            return true;
        }

        public override FlowBlock getSplitPoint() => (sizeOut() != 2) ? null : this;

        public override int flipInPlaceTest(List<PcodeOp> fliplist)
        {
            if (0 == op.Count) {
                return 2;
            }
            PcodeOp lastop = op.Last.Value;
            return (lastop.code() != OpCode.CPUI_CBRANCH)
                ? 2
                : Funcdata.opFlipInPlaceTest(lastop, fliplist);
        }

        public override void flipInPlaceExecute()
        {
            PcodeOp lastop = op.Last.Value;
            // This is similar to negateCondition but we don't need to set the boolean_flip flag on lastop
            // because it is getting explicitly changed
            // Flip whether the fallthru block is true/false
            lastop.flipFlag(PcodeOp.Flags.fallthru_true);
            // Flip the order of outof this
            base.negateCondition(true);
        }

        public override bool isComplex()
        {
            PcodeOp inst, d_op;
            Varnode vn;
            int statement;
            int maxref;

            // Is this block too complicated for a condition.
            // We count the number of statements in the block
            statement = 0;
            if (sizeOut() >= 2) {
                // Consider the branch as a statement
                statement = 1;
            }
            // Max number of uses a varnode can have
            // before it must be considered an explicit variable
            maxref = data.getArch().max_implied_ref;
            foreach (PcodeOp iter in op) {
                inst = iter;
                if (inst.isMarker()) {
                    continue;
                }
                vn = inst.getOut();
                if (inst.isCall()) {
                    statement += 1;
                }
                else if (vn == null) {
                    if (inst.isFlowBreak()) {
                        continue;
                    }
                    statement += 1;
                }
                else {
                    // If the operation is a calculation with output
                    // This is a conservative version of 
                    // Varnode::calc_explicit
                    bool yesstatement = false;
                    if (vn.hasNoDescend()) {
                        yesstatement = true;
                    }
                    else if (vn.isAddrTied()) {
                        // Being conservative
                        yesstatement = true;
                    }
                    else {
                        int totalref = 0;

                        IEnumerator<PcodeOp> iter2 = vn.beginDescend();
                        while (iter2.MoveNext()) {
                            d_op = iter2.Current;
                            if (d_op.isMarker() || (d_op.getParent() != this)) {
                                // Variable used outside of block
                                yesstatement = true;
                                break;
                            }
                            totalref += 1;
                            if (totalref > maxref) {
                                // If used too many times
                                // consider defining op as a statement
                                yesstatement = true;
                                break;
                            }
                        }
                    }
                    if (yesstatement) {
                        statement += 1;
                    }
                }
                if (statement > 2) {
                    return true;
                }
            }
            return false;
        }

        /// Check if \b this block can be removed without introducing inconsistencies
        /// Does removing this block leads to redundant MULTIEQUAL entries which are inconsistent.
        /// A MULTIEQUAL can hide an implied copy, in which case \b this block is actually doing something
        /// and shouldn't be removed.
        /// \param outslot is the index of the outblock that \b this is getting collapsed to
        /// \return true if there is no implied COPY
        public bool unblockedMulti(int outslot)
        {
            BlockBasic blout = (BlockBasic)getOut(outslot);
            FlowBlock bl;
            PcodeOp multiop;
            PcodeOp othermulti;
            Varnode vnremove;
            Varnode vnredund;

            // First we build list of blocks which would have
            // redundant branches into blout
            List<FlowBlock> redundlist = new List<FlowBlock>();
            for (int i = 0; i < sizeIn(); ++i) {
                bl = getIn(i);
                for (int j = 0; j < bl.sizeOut(); ++j) {
                    if (bl.getOut(j) == blout) {
                        redundlist.Add(bl);
                    }
                }
                // We assume blout appears only once in bl's and this's outlists
            }
            if (0 == redundlist.Count) {
                return true;
            }
            foreach (PcodeOp iter in blout.op) {
                multiop = iter;
                if (multiop.code() != OpCode.CPUI_MULTIEQUAL) {
                    continue;
                }
                foreach (FlowBlock biter in redundlist) {
                    bl = biter;
                    // One of the redundant varnodes
                    vnredund = multiop.getIn(blout.getInIndex(bl));
                    vnremove = multiop.getIn(blout.getInIndex(this)) ?? throw new ApplicationException();
                    if (vnremove.isWritten()) {
                        othermulti = vnremove.getDef() ?? throw new ApplicationException();
                        if ((othermulti.code() == OpCode.CPUI_MULTIEQUAL)
                            && (othermulti.getParent() == this))
                        {
                            vnremove = othermulti.getIn(getInIndex(bl));
                        }
                    }
                    if (vnremove != vnredund) {
                        // Redundant branches must be identical
                        return false;
                    }
                }
            }
            return true;
        }

        /// Does \b this block contain only MULTIEQUAL and INDIRECT ops
        /// This is a crucial test for whether \b this block is doing anything substantial
        /// or is a candidate for removal.  Even blocks that "do nothing" have some kind of branch
        /// and placeholder operations (MULTIEQUAL and INDIRECT) for data flowing through the block.
        /// This tests if there is any other operation going on.
        /// \return \b true if there only MULTIEQUAL, INDIRECT, and branch operations in \b this
        public bool hasOnlyMarkers()
        {
            // (and a branch)
            foreach (PcodeOp iter in op) {
                PcodeOp bop = iter;
                if (bop.isMarker()) {
                    continue;
                }
                if (bop.isBranch()) {
                    continue;
                }
                return false;
            }
            return true;
        }

        /// Should \b this block should be removed
        /// Check if \b this block is doing anything useful.
        /// \return \b true if the block does nothing and should be removed
        public bool isDoNothing()
        {
            if (sizeOut() != 1) {
                // A block that does nothing useful
                // has exactly one out, (no return or cbranch)
                return false;
            }
            if (sizeIn() == 0) {
                // A block that does nothing but
                // is a starting block, may need to be a
                // placeholder for global(persistent) vars
                return false;
            }
            if ((sizeIn() == 1) && (getIn(0).isSwitchOut())) {
                if (getOut(0).sizeIn() > 1) {
                    // Don't remove switch targets
                    return false;
                }
            }
            PcodeOp? lastop = lastOp();
            return ((lastop == null) || (lastop.code() != OpCode.CPUI_BRANCHIND) || hasOnlyMarkers());
        }

        /// Return an iterator to the beginning of the PcodeOps
        public LinkedListNode<PcodeOp>? beginOp() => (0 == op.Count) ? null : op.First;

        //public IBiDirEnumerator<PcodeOp> GetBiDirectionalEnumerator(bool reverseOrder = false)
        //    => op.GetBiDirectionalEnumerator(reverseOrder); 

        // Return an enumerator in reverse order</summary>
        public LinkedListNode<PcodeOp>? reverseEnumerator() => op.Last;

        // list<PcodeOp>::iterator endOp(void) { return op.end(); }       ///< Return an iterator to the end of the PcodeOps
        // IEnumerator<PcodeOp> beginOp(void) { return op.begin(); }	///< Return an iterator to the beginning of the PcodeOps

        // Return an iterator to the end of the PcodeOps
        public LinkedListNode<PcodeOp>? endOp() => (0 == op.Count) ? null : op.Last;

        /// Return \b true if \b block contains no operations
        public bool emptyOp() => (0 == op.Count);

        /// \brief Check if there is meaningful activity between two branch instructions
        ///
        /// The first branch is assumed to be a CBRANCH one edge of which flows into
        /// the other branch. The flow can be through 1 or 2 blocks.  If either block
        /// performs an operation other than MULTIEQUAL, INDIRECT (or the branch), then
        /// return \b false.
        /// \param first is the CBRANCH operation
        /// \param path is the index of the edge to follow to the other branch
        /// \param last is the other branch operation
        /// \return \b true if there is no meaningful activity
        public static bool noInterveningStatement(PcodeOp first, int path, PcodeOp last)
        {
            BlockBasic curbl = (BlockBasic)first.getParent().getOut(path);
            for (int i = 0; i < 2; ++i) {
                if (!curbl.hasOnlyMarkers()) {
                    return false;
                }
                if (curbl != last.getParent()) {
                    if (curbl.sizeOut() != 1) {
                        // Intervening conditional branch
                        return false;
                    }
                }
                else {
                    return true;
                }
                curbl = (BlockBasic)curbl.getOut(0);
            }
            return false;
        }

        /// Find MULTIEQUAL with given inputs
        /// If there exists a OpCode.CPUI_MULTIEQUAL PcodeOp in \b this basic block that takes the given exact list of Varnodes
        /// as its inputs, return that PcodeOp. Otherwise return null.
        /// \param varArray is the exact list of Varnodes
        /// \return the MULTIEQUAL or null
        public PcodeOp? findMultiequal(List<Varnode> varArray)
        {
            Varnode vn = varArray[0];
            PcodeOp op;
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (true) {
                op = iter.Current;
                if (op.code() == OpCode.CPUI_MULTIEQUAL && op.getParent() == this) {
                    break;
                }
                if (!iter.MoveNext()) {
                    return null;
                }
            }
            for (int i = 0; i < op.numInput(); ++i) {
                if (op.getIn(i) != varArray[i]) {
                    return null;
                }
            }
            return op;
        }

        /// Verify given Varnodes are defined with same PcodeOp
        /// Each Varnode must be defined by a PcodeOp with the same OpCode.  The Varnode, within the array, is replaced
        /// with the input Varnode in the indicated slot.
        /// \param varArray is the given array of Varnodes
        /// \param slot is the indicated slot
        /// \return \b true if all the Varnodes are defined in the same way
        public static bool liftVerifyUnroll(List<Varnode> varArray, int slot)
        {
            OpCode opc;
            Varnode? cvn;
            Varnode vn = varArray[0];
            if (!vn.isWritten()) {
                return false;
            }
            PcodeOp op = vn.getDef() ?? throw new BugException();
            opc = op.code();
            if (op.numInput() == 2) {
                cvn = op.getIn(1 - slot);
                if (!cvn.isConstant()) {
                    return false;
                }
            }
            else {
                cvn = null;
            }
            varArray[0] = op.getIn(slot);
            for (int i = 1; i < varArray.Count; ++i) {
                vn = varArray[i];
                if (!vn.isWritten()) {
                    return false;
                }
                op = vn.getDef() ?? throw new BugException();
                if (op.code() != opc) {
                    return false;
                }

                if (cvn != null) {
                    Varnode cvn2 = op.getIn(1 - slot);
                    if (!cvn2.isConstant()) {
                        return false;
                    }
                    if (cvn.getSize() != cvn2.getSize()) {
                        return false;
                    }
                    if (cvn.getOffset() != cvn2.getOffset()) {
                        return false;
                    }
                }
                varArray[i] = op.getIn(slot);
            }
            return true;
        }
    }
}
