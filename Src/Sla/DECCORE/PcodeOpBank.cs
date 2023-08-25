using Sla.CORE;

using PcodeOpTree = System.Collections.Generic.Dictionary<Sla.CORE.SeqNum, Sla.DECCORE.PcodeOp>;

namespace Sla.DECCORE
{
    /// <summary>Container class for PcodeOps associated with a single function.
    /// The PcodeOp objects are maintained under multiple different sorting criteria to
    /// facilitate quick access in various situations. The main sort (PcodeOpTree) is by
    /// sequence number (SeqNum). PcodeOps are also grouped into \e alive and \e dead lists
    /// to distinguish between raw p-code ops and those that are fully linked into control-flow.
    /// Several lists group PcodeOps with important op-codes (like STORE and RETURN).</summary>
    /// <remarks>A single PcodeOp should be either in the deadlist or the alivelist, not in both.</remarks>
    internal class PcodeOpBank
    {
        /// The main sequence number sort
        private PcodeOpTree optree;
        /// List of \e dead PcodeOps
        private LinkedList<PcodeOp> deadlist = new LinkedList<PcodeOp>();
        /// List of \e alive PcodeOps
        private LinkedList<PcodeOp> alivelist = new LinkedList<PcodeOp>();
        /// List of STORE PcodeOps
        private List<PcodeOp> storelist = new List<PcodeOp>();
        /// list of LOAD PcodeOps
        private List<PcodeOp> loadlist = new List<PcodeOp>();
        /// List of RETURN PcodeOps
        private List<PcodeOp> returnlist = new List<PcodeOp>();
        /// List of user-defined PcodeOps
        private List<PcodeOp> useroplist = new List<PcodeOp>();
        /// List of retired PcodeOps
        private List<PcodeOp> deadandgone = new List<PcodeOp>();
        /// Counter for producing unique id's for each op
        private uint uniqid;

        /// Add given PcodeOp to specific op-code list
        /// Add the PcodeOp to the list of ops with the same op-code. Currently only certain
        /// op-codes have a dedicated list.
        /// \param op is the given PcodeOp
        private void addToCodeList(PcodeOp op)
        {
            switch (op.code()) {
                case OpCode.CPUI_STORE:
                    // op.codeiter = storelist.Add(op);
                    op._codePosition = storelist.Count;
                    storelist.Add(op);
                    break;
                case OpCode.CPUI_LOAD:
                    // op.codeiter = loadlist.Add(op);
                    op._codePosition = loadlist.Count;
                    loadlist.Add(op);
                    break;
                case OpCode.CPUI_RETURN:
                    // op.codeiter = returnlist.Add(op);
                    op._codePosition = returnlist.Count;
                    returnlist.Add(op);
                    break;
                case OpCode.CPUI_CALLOTHER:
                    // op.codeiter = useroplist.Add(op);
                    op._codePosition = useroplist.Count;
                    useroplist.Add(op);
                    break;
                default:
                    break;
            }
        }

        /// Remove given PcodeOp from specific op-code list
        /// Remove the PcodeOp from its list of ops with the same op-code. Currently only certain
        /// op-codes have a dedicated list.
        /// \param op is the given PcodeOp
        private void removeFromCodeList(PcodeOp op)
        {
            switch (op.code()) {
                case OpCode.CPUI_STORE:
                    storelist.Remove(op);
                    break;
                case OpCode.CPUI_LOAD:
                    loadlist.Remove(op);
                    break;
                case OpCode.CPUI_RETURN:
                    returnlist.Remove(op);
                    break;
                case OpCode.CPUI_CALLOTHER:
                    useroplist.Remove(op);
                    break;
                default:
                    break;
            }
        }

        /// Clear all op-code specific lists
        private void clearCodeLists()
        {
            storelist.Clear();
            loadlist.Clear();
            returnlist.Clear();
            useroplist.Clear();
        }

        /// Clear all PcodeOps from \b this container
        public void clear()
        {
            //IEnumerator<PcodeOp> iter;

            //for (iter = alivelist.begin(); iter != alivelist.end(); ++iter)
            //    delete* iter;
            //for (iter = deadlist.begin(); iter != deadlist.end(); ++iter)
            //    delete* iter;
            //for (iter = deadandgone.begin(); iter != deadandgone.end(); ++iter)
            //    delete* iter;
            optree.Clear();
            alivelist.Clear();
            deadlist.Clear();
            clearCodeLists();
            deadandgone.Clear();
            uniqid = 0;
        }

        public PcodeOpBank()
        {
            uniqid = 0;
        }
        
        ~PcodeOpBank()
        {
            clear();
        }

        /// Set the unique id counter
        public void setUniqId(uint val)
        {
            uniqid = val;
        }

        /// Get the next unique id
        public uint getUniqId() => uniqid;

        /// Create a PcodeOp with at a given Address
        /// A new PcodeOp is allocated with the indicated number of input slots, which
        /// start out empty. A sequence number is assigned, and the op is added to the
        /// end of the \e dead list.
        /// \param inputs is the number of input slots
        /// \param pc is the Address to associate with the PcodeOp
        /// \return the newly allocated PcodeOp
        public PcodeOp create(int inputs, Address pc)
        {
            PcodeOp op = new PcodeOp(inputs, new SeqNum(pc, uniqid++));
            optree[op.getSeqNum()] = op;
            // Start out life as dead
            op.setFlag(PcodeOp.Flags.dead);
            // op.insertiter = deadlist.Add(op);
            deadlist.AddLast(op._deadAliveNode = new LinkedListNode<PcodeOp>(op));
            return op;
        }

        /// Create a PcodeOp with a given sequence number
        /// A new PcodeOp is allocated with the indicated number of input slots and the
        /// specific sequence number, suitable for cloning and restoring from XML.
        /// The op is added to the end of the \e dead list.
        /// \param inputs is the number of input slots
        /// \param sq is the specified sequence number
        /// \return the newly allocated PcodeOp
        public PcodeOp create(int inputs, SeqNum sq)
        {
            PcodeOp op = new PcodeOp(inputs, sq);
            if (sq.getTime() >= uniqid)
                uniqid = sq.getTime() + 1;

            optree[op.getSeqNum()] = op;
            op.setFlag(PcodeOp.Flags.dead);     // Start out life as dead
            // op.insertiter = deadlist.Add(op);
            deadlist.AddLast(op._deadAliveNode = new LinkedListNode<PcodeOp>(op));
            return op;
        }

        /// Destroy/retire the given PcodeOp
        /// The given PcodeOp is removed from all internal lists and added to a final
        /// \e deadandgone list. The memory is not reclaimed until the whole container is
        /// destroyed, in case pointer references still exist.  These will all still
        /// be marked as \e dead.
        /// \param op is the given PcodeOp to destroy
        public void destroy(PcodeOp op)
        {
            if (!op.isDead())
                throw new LowlevelError("Deleting integrated op");

            optree.Remove(op.getSeqNum());
            deadlist.Remove(op);
            removeFromCodeList(op);
            deadandgone.Add(op);
        }

        /// Destroy/retire all PcodeOps in the \e dead list
        public void destroyDead()
        {
            foreach (PcodeOp op in deadlist) {
                destroy(op);
            }
        }

        /// Change the op-code for the given PcodeOp
        /// The PcodeOp is assigned the new op-code, which may involve moving it
        /// between the internal op-code specific lists.
        /// \param op is the given PcodeOp to change
        /// \param newopc is the new op-code object
        public void changeOpcode(PcodeOp op, TypeOp newopc)
        {
            if (op.opcode != (TypeOp)null)
                removeFromCodeList(op);
            op.setOpcode(newopc);
            addToCodeList(op);
        }

        /// Mark the given PcodeOp as \e alive
        /// The PcodeOp is moved out of the \e dead list into the \e alive list. The PcodeOp.isDead() method
        /// will now return \b false.
        /// \param op is the given PcodeOp to mark
        public void markAlive(PcodeOp op)
        {
            deadlist.Remove(op._deadAliveNode);
            op.clearFlag(PcodeOp.Flags.dead);
            // op.insertiter = alivelist.Add(op);
            alivelist.AddLast(op._deadAliveNode);
        }

        /// Mark the given PcodeOp as \e dead
        /// The PcodeOp is moved out of the \e alive list into the \e dead list. The PcodeOp.isDead() method
        /// will now return \b true.
        /// \param op is the given PcodeOp to mark
        public void markDead(PcodeOp op)
        {
            alivelist.Remove(op._deadAliveNode);
            op.setFlag(PcodeOp.Flags.dead);
            // op.insertiter = deadlist.Add(op);
            deadlist.AddLast(op._deadAliveNode);
        }

        /// Insert the given PcodeOp after a point in the \e dead list
        /// The op is moved to right after a specified op in the \e dead list.
        /// \param op is the given PcodeOp to move
        /// \param prev is the specified op in the \e dead list
        public void insertAfterDead(PcodeOp op, PcodeOp prev)
        {
            if (!op.isDead() || !prev.isDead())
                throw new LowlevelError("Dead move called on ops which aren't dead");
            deadlist.Remove(op._deadAliveNode);
            // IEnumerator<PcodeOp> iter = prev.insertiter;
            // ++iter;
            // op.insertiter = deadlist.insert(iter, op);
            deadlist.AddAfter(prev._deadAliveNode, op._deadAliveNode);
        }

        /// \brief Move a sequence of PcodeOps to a point in the \e dead list.
        /// The point is right after a provided op. All ops must be in the \e dead list.
        /// \param firstop is the first PcodeOp in the sequence to be moved
        /// \param lastop is the last PcodeOp in the sequence to be moved
        /// \param prev is the provided point to move to
        public void moveSequenceDead(PcodeOp firstop, PcodeOp lastop, PcodeOp prev)
        {
            // IEnumerator<PcodeOp> enditer = lastop.insertiter;
            LinkedListNode<PcodeOp>? enditer = lastop._deadAliveNode;
            // ++enditer;
            enditer = enditer.Next;
            // IEnumerator<PcodeOp> previter = prev.insertiter;
            LinkedListNode<PcodeOp>? previter = prev._deadAliveNode;
            // ++previter;
            previter = previter.Next;
            // if (previter != firstop.insertiter)
            if (!object.ReferenceEquals(previter, firstop._deadAliveNode))
                // Check for degenerate move
                deadlist.splice(previter, deadlist, firstop._deadAliveNode, enditer);
        }

        /// Mark any COPY ops in the given range as \e incidental
        /// Incidental COPYs are not considered active use of parameter passing Varnodes by
        /// parameter analysis algorithms.
        /// \param firstop is the start of the range of incidental COPY ops
        /// \param lastop is the end of the range of incidental COPY ops
        public void markIncidentalCopy(PcodeOp firstop, PcodeOp lastop)
        {
            if (   (null != lastop)
                && !object.ReferenceEquals(firstop._deadAliveNode.List, lastop._deadAliveNode.List))
            {
                throw new ArgumentException();
            }
            LinkedListNode<PcodeOp>? scannedNode = firstop._deadAliveNode;
            do {
                PcodeOp op = scannedNode.Value;
                if (op.code() == OpCode.CPUI_COPY)
                    op.setAdditionalFlag(PcodeOp.AdditionalFlags.incidental_copy);
                scannedNode = scannedNode.Next;
                if ((null == scannedNode) && (null != lastop)) {
                    throw new ArgumentException();
                }
            } while (!object.ReferenceEquals(scannedNode.Value, lastop));
        }

        /// Return \b true if there are no PcodeOps in \b this container
        public bool empty() => optree.empty();

        /// Find the first executing PcodeOp for a target address
        /// Find the first PcodeOp at or after the given Address assuming they have not
        /// yet been broken up into basic blocks. Take into account delay slots.
        /// \param addr is the given Address
        /// \return the targeted PcodeOp (or NULL)
        public PcodeOp? target(Address addr)
        {
            PcodeOpTree::const_iterator iter = optree.lower_bound(new SeqNum(addr, 0));
            if (iter == optree.end()) return (PcodeOp)null;
            return (*iter).second.target();
        }

        /// Find a PcodeOp by sequence number
        /// \param num is the given sequence number
        /// \return the matching PcodeOp (or NULL)
        public PcodeOp? findOp(SeqNum num)
        {
            PcodeOp? result;
            return (optree.TryGetValue(num, out result)) ? result : null;
        }

        /// Find the PcodeOp considered a \e fallthru of the given PcodeOp
        /// The term \e fallthru in this context refers to p-code \e not assembly instructions.
        /// \param op is the given PcodeOp
        /// \return the fallthru PcodeOp
        public PcodeOp fallthru(PcodeOp op)
        {
            PcodeOp retop;
            if (op.isDead()) {
                // In this case we know an instruction is contiguous in the dead list
                LinkedListNode<PcodeOp> iter = op._deadAliveNode;
                //IEnumerator<PcodeOp> iter = op.insertiter;
                //++iter;
                if (null != iter.Next) {
                    retop = iter.Next.Value;
                    if (!retop.isInstructionStart()) // If the next in dead list is not marked
                        return retop;       // It is in the same instruction, and is the fallthru
                }
                // --iter;
                SeqNum max = op.getSeqNum();
                // Find start of instruction
                while (!iter.Value.isInstructionStart()) {
                    // --iter;
                    iter = iter.Previous ?? throw new ApplicationException();
                }
                // Find biggest sequence number in this instruction
                // This is probably -op- itself because it is the
                // last op in the instruction, but it might not be
                // because of delay slot reordering
                while ((null != iter) && (iter.Value != op)) {
                    if (max < iter.Value.getSeqNum())
                        max = iter.Value.getSeqNum();
                    iter = iter.Next;
                }
                PcodeOpTree::const_iterator nextiter = optree.upper_bound(max);
                if (nextiter == optree.end()) return (PcodeOp)null;
                retop = (*nextiter).second;
                return retop;
            }
            return op.nextOp();
        }

        /// \brief Start of all PcodeOps in sequence number order
        public PcodeOpTree::const_iterator beginAll() => optree.begin();

        /// \brief End of all PcodeOps in sequence number order
        public PcodeOpTree::const_iterator endAll() => optree.end();

        /// \brief Start of all PcodeOps at one Address
        public PcodeOpTree::const_iterator begin(Address addr)
        {
            return optree.lower_bound(new SeqNum(addr, 0));
        }

        /// \brief End of all PcodeOps at one Address
        public PcodeOpTree::const_iterator end(Address addr)
        {
            return optree.upper_bound(new SeqNum(addr, uint.MaxValue));
        }

        /// \brief Start of all PcodeOps marked as \e alive
        public IEnumerator<PcodeOp> beginAlive() => alivelist.GetEnumerator();

        ///// \brief End of all PcodeOps marked as \e alive
        //public IEnumerator<PcodeOp> endAlive() => alivelist.end();

        /// \brief Start of all PcodeOps marked as \e dead
        public IEnumerator<PcodeOp> beginDead() => deadlist.GetEnumerator();
        
        public IEnumerator<PcodeOp> beginReverseDead() => deadlist.GetReverseEnumerator();

        /// \brief End of all PcodeOps marked as \e dead
        public IEnumerator<PcodeOp> endDead() => deadlist.end();

        /// \brief Start of all PcodeOps sharing the given op-code
        public IEnumerator<PcodeOp> begin(OpCode opc)
        {
            switch (opc) {
                case OpCode.CPUI_STORE:
                    return storelist.GetEnumerator();
                case OpCode.CPUI_LOAD:
                    return loadlist.GetEnumerator();
                case OpCode.CPUI_RETURN:
                    return returnlist.GetEnumerator();
                case OpCode.CPUI_CALLOTHER:
                    return useroplist.GetEnumerator();
                default:
                    break;
            }
            return alivelist.end();
        }

        ///// \brief End of all PcodeOps sharing the given op-code
        //public IEnumerator<PcodeOp> end(OpCode opc)
        //{
        //    switch (opc) {
        //        case OpCode.CPUI_STORE:
        //            return storelist.end();
        //        case OpCode.CPUI_LOAD:
        //            return loadlist.end();
        //        case OpCode.CPUI_RETURN:
        //            return returnlist.end();
        //        case OpCode.CPUI_CALLOTHER:
        //            return useroplist.end();
        //        default:
        //            break;
        //    }
        //    return alivelist.end();
        //}

        /// \brief Try to determine if \b vn1 and \b vn2 contain the same value
        ///
        /// Return:
        ///    -  -1, if they do \b not, or if it can't be immediately verified
        ///    -   0, if they \b do hold the same value
        ///    -  >0, if the result is contingent on additional varnode pairs having the same value
        /// In the last case, the varnode pairs are returned as (res1[i],res2[i]),
        /// where the return value is the number of pairs.
        /// \param vn1 is the first Varnode to compare
        /// \param vn2 is the second Varnode
        /// \param res1 is a reference to the first returned Varnode
        /// \param res2 is a reference to the second returned Varnode
        /// \return the result of the comparison
        internal static int functionalEqualityLevel(Varnode vn1, Varnode vn2, Varnode[] res1, Varnode[] res2)
        {
            int testval = functionalEqualityLevel0(vn1, vn2);
            if (testval != 1) return testval;
            PcodeOp op1 = vn1.getDef() ?? throw new BugException();
            PcodeOp op2 = vn2.getDef() ?? throw new BugException();
            OpCode opc = op1.code();

            if (opc != op2.code()) return -1;

            int num = op1.numInput();
            if (num != op2.numInput()) return -1;
            if (op1.isMarker()) return -1;
            if (op2.isCall()) return -1;
            if (opc == OpCode.CPUI_LOAD) {
                // FIXME: We assume two loads produce the same
                // result if the address is the same and the loads
                // occur in the same instruction
                if (op1.getAddr() != op2.getAddr()) return -1;
            }
            if (num >= 3) {
                if (opc != OpCode.CPUI_PTRADD) return -1; // If this is a PTRADD
                if (op1.getIn(2).getOffset() != op2.getIn(2).getOffset()) return -1; // Make sure the elsize constant is equal
                num = 2;            // Otherwise treat as having 2 inputs
            }
            for (int i = 0; i < num; ++i) {
                res1[i] = op1.getIn(i);
                res2[i] = op2.getIn(i);
            }

            testval = functionalEqualityLevel0(res1[0], res2[0]);
            if (testval == 0) {
                // A match locks in this comparison ordering
                if (num == 1) return 0;
                testval = functionalEqualityLevel0(res1[1], res2[1]);
                if (testval == 0) return 0;
                if (testval < 0) return -1;
                res1[0] = res1[1];      // Match is contingent on second pair
                res2[0] = res2[1];
                return 1;
            }
            if (num == 1) return testval;
            int testval2 = functionalEqualityLevel0(res1[1], res2[1]);
            if (testval2 == 0) {
                // A match locks in this comparison ordering
                return testval;
            }
            int unmatchsize = ((testval == 1) && (testval2 == 1)) ? 2 : -1;

            if (!op1.isCommutative()) return unmatchsize;
            // unmatchsize must be 2 or -1 here on a commutative operator,
            // try flipping
            int comm1 = functionalEqualityLevel0(res1[0], res2[1]);
            int comm2 = functionalEqualityLevel0(res1[1], res2[0]);
            if ((comm1 == 0) && (comm2 == 0))
                return 0;
            if ((comm1 < 0) || (comm2 < 0))
                return unmatchsize;
            if (comm1 == 0) {
                // AND (comm2==1)
                res1[0] = res1[1];      // Left over unmatch is res1[1] and res2[0]
                return 1;
            }
            if (comm2 == 0) {
                // AND (comm1==1)
                res2[0] = res2[1];      // Left over unmatch is res1[0] and res2[1]
                return 1;
            }
            // If we reach here (comm1==1) AND (comm2==1)
            if (unmatchsize == 2)       // If the original ordering wasn't impossible
                return 2;           // Prefer the original ordering
            Varnode tmpvn = res2[0];   // Otherwise swap the ordering
            res2[0] = res2[1];
            res2[1] = tmpvn;
            return 2;
        }

        /// \brief Determine if two Varnodes hold the same value
        ///
        /// Only return \b true if it can be immediately determined they are equivalent
        /// \param vn1 is the first Varnode
        /// \param vn2 is the second Varnode
        /// \return true if they are provably equal
        internal static bool functionalEquality(Varnode vn1, Varnode vn2)

        {
            Varnode[] buf1 = new Varnode[2];
            Varnode[] buf2 = new Varnode[2];
            return (PcodeOpBank.functionalEqualityLevel(vn1, vn2, buf1, buf2) == 0);
        }

        /// \brief Return true if vn1 and vn2 are verifiably different values
        ///
        /// This is actually a rather speculative test
        /// \param vn1 is the first Varnode to compare
        /// \param vn2 is the second Varnode
        /// \param depth is the maximum level to recurse while testing
        /// \return \b true if they are different
        internal static bool functionalDifference(Varnode vn1, Varnode vn2, int depth)
        {
            if (vn1 == vn2) return false;
            if (!vn1.isWritten() || !vn2.isWritten()) {
                if (vn1.isConstant() && vn2.isConstant())
                    return !(vn1.getAddr() == vn2.getAddr());
                if (vn1.isInput() && vn2.isInput()) return false; // Might be the same
                if (vn1.isFree() || vn2.isFree()) return false; // Might be the same
                return true;
            }
            PcodeOp op1 = vn1.getDef() ?? throw new BugException();
            PcodeOp op2 = vn2.getDef() ?? throw new BugException();
            if (op1.code() != op2.code()) return true;
            int num = op1.numInput();
            if (num != op2.numInput()) return true;
            if (depth == 0) return true;    // Different as far as we can tell
            depth -= 1;
            for (int i = 0; i < num; ++i)
                if (functionalDifference(op1.getIn(i), op2.getIn(i), depth))
                    return true;
            return false;
        }

        private static int functionalEqualityLevel0(Varnode vn1, Varnode vn2)
        {
            // Return 0 if -vn1- and -vn2- must hold same value
            // Return -1 if they definitely don't hold same value
            // Return 1 if the same value depends on ops writing to -vn1- and -vn2-
            if (vn1 == vn2) return 0;
            if (vn1.getSize() != vn2.getSize()) return -1;
            if (vn1.isConstant()) {
                return !vn2.isConstant() ? -1 : (vn1.getOffset() == vn2.getOffset()) ? 0 : -1;
            }
            if (vn2.isConstant()) return -1;
            return (vn1.isWritten() && vn2.isWritten()) ? 1 : -1;
        }
    }
}
