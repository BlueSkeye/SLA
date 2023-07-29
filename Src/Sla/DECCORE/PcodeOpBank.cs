using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Sla.DECCORE
{
    /// \brief Container class for PcodeOps associated with a single function
    ///
    /// The PcodeOp objects are maintained under multiple different sorting criteria to
    /// facilitate quick access in various situations. The main sort (PcodeOpTree) is by
    /// sequence number (SeqNum). PcodeOps are also grouped into \e alive and \e dead lists
    /// to distinguish between raw p-code ops and those that are fully linked into control-flow.
    /// Several lists group PcodeOps with important op-codes (like STORE and RETURN).
    internal class PcodeOpBank
    {
        /// The main sequence number sort
        private PcodeOpTree optree;
        /// List of \e dead PcodeOps
        private List<PcodeOp> deadlist;
        /// List of \e alive PcodeOps
        private List<PcodeOp> alivelist;
        /// List of STORE PcodeOps
        private List<PcodeOp> storelist;
        /// list of LOAD PcodeOps
        private List<PcodeOp> loadlist;
        /// List of RETURN PcodeOps
        private List<PcodeOp> returnlist;
        /// List of user-defined PcodeOps
        private List<PcodeOp> useroplist;
        /// List of retired PcodeOps
        private List<PcodeOp> deadandgone;
        /// Counter for producing unique id's for each op
        private uint uniqid;

        /// Add given PcodeOp to specific op-code list
        /// Add the PcodeOp to the list of ops with the same op-code. Currently only certain
        /// op-codes have a dedicated list.
        /// \param op is the given PcodeOp
        private void addToCodeList(PcodeOp op)
        {
            switch (op.code())
            {
                case CPUI_STORE:
                    op.codeiter = storelist.insert(storelist.end(), op);
                    break;
                case CPUI_LOAD:
                    op.codeiter = loadlist.insert(loadlist.end(), op);
                    break;
                case CPUI_RETURN:
                    op.codeiter = returnlist.insert(returnlist.end(), op);
                    break;
                case CPUI_CALLOTHER:
                    op.codeiter = useroplist.insert(useroplist.end(), op);
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
            switch (op.code())
            {
                case CPUI_STORE:
                    storelist.erase(op.codeiter);
                    break;
                case CPUI_LOAD:
                    loadlist.erase(op.codeiter);
                    break;
                case CPUI_RETURN:
                    returnlist.erase(op.codeiter);
                    break;
                case CPUI_CALLOTHER:
                    useroplist.erase(op.codeiter);
                    break;
                default:
                    break;
            }
        }

        /// Clear all op-code specific lists
        private void clearCodeLists()
        {
            storelist.clear();
            loadlist.clear();
            returnlist.clear();
            useroplist.clear();
        }

        /// Clear all PcodeOps from \b this container
        public void clear()
        {
            list<PcodeOp*>::iterator iter;

            for (iter = alivelist.begin(); iter != alivelist.end(); ++iter)
                delete* iter;
            for (iter = deadlist.begin(); iter != deadlist.end(); ++iter)
                delete* iter;
            for (iter = deadandgone.begin(); iter != deadandgone.end(); ++iter)
                delete* iter;
            optree.clear();
            alivelist.clear();
            deadlist.clear();
            clearCodeLists();
            deadandgone.clear();
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
        /// Create a PcodeOp with a given sequence number
        /// A new PcodeOp is allocated with the indicated number of input slots, which
        /// start out empty.  A sequence number is assigned, and the op is added to the
        /// end of the \e dead list.
        /// \param inputs is the number of input slots
        /// \param pc is the Address to associate with the PcodeOp
        /// \return the newly allocated PcodeOp
        public PcodeOp create(int inputs, Address pc)
        {
            PcodeOp* op = new PcodeOp(inputs, SeqNum(pc, uniqid++));
            optree[op.getSeqNum()] = op;
            op.setFlag(PcodeOp::dead);     // Start out life as dead
            op.insertiter = deadlist.insert(deadlist.end(), op);
            return op;
        }

        /// A new PcodeOp is allocated with the indicated number of input slots and the
        /// specific sequence number, suitable for cloning and restoring from XML.
        /// The op is added to the end of the \e dead list.
        /// \param inputs is the number of input slots
        /// \param sq is the specified sequence number
        /// \return the newly allocated PcodeOp
        public PcodeOp create(int inputs, SeqNum sq)
        {
            PcodeOp* op;
            op = new PcodeOp(inputs, sq);
            if (sq.getTime() >= uniqid)
                uniqid = sq.getTime() + 1;

            optree[op.getSeqNum()] = op;
            op.setFlag(PcodeOp::dead);     // Start out life as dead
            op.insertiter = deadlist.insert(deadlist.end(), op);
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

            optree.erase(op.getSeqNum());
            deadlist.erase(op.insertiter);
            removeFromCodeList(op);
            deadandgone.Add(op);
        }

        /// Destroy/retire all PcodeOps in the \e dead list
        public void destroyDead()
        {
            list<PcodeOp*>::iterator iter;
            PcodeOp* op;

            iter = deadlist.begin();
            while (iter != deadlist.end())
            {
                op = *iter++;
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
            if (op.opcode != (TypeOp*)0)
                removeFromCodeList(op);
            op.setOpcode(newopc);
            addToCodeList(op);
        }

        /// Mark the given PcodeOp as \e alive
        /// The PcodeOp is moved out of the \e dead list into the \e alive list.  The
        /// PcodeOp::isDead() method will now return \b false.
        /// \param op is the given PcodeOp to mark
        public void markAlive(PcodeOp op)
        {
            deadlist.erase(op.insertiter);
            op.clearFlag(PcodeOp::dead);
            op.insertiter = alivelist.insert(alivelist.end(), op);
        }

        /// Mark the given PcodeOp as \e dead
        /// The PcodeOp is moved out of the \e alive list into the \e dead list. The
        /// PcodeOp::isDead() method will now return \b true.
        /// \param op is the given PcodeOp to mark
        public void markDead(PcodeOp op)
        {
            alivelist.erase(op.insertiter);
            op.setFlag(PcodeOp::dead);
            op.insertiter = deadlist.insert(deadlist.end(), op);
        }

        /// Insert the given PcodeOp after a point in the \e dead list
        /// The op is moved to right after a specified op in the \e dead list.
        /// \param op is the given PcodeOp to move
        /// \param prev is the specified op in the \e dead list
        public void insertAfterDead(PcodeOp op, PcodeOp prev)
        {
            if ((!op.isDead()) || (!prev.isDead()))
                throw new LowlevelError("Dead move called on ops which aren't dead");
            deadlist.erase(op.insertiter);
            list<PcodeOp*>::iterator iter = prev.insertiter;
            ++iter;
            op.insertiter = deadlist.insert(iter, op);
        }

        /// \brief Move a sequence of PcodeOps to a point in the \e dead list.
        ///
        /// The point is right after a provided op. All ops must be in the \e dead list.
        /// \param firstop is the first PcodeOp in the sequence to be moved
        /// \param lastop is the last PcodeOp in the sequence to be moved
        /// \param prev is the provided point to move to
        public void moveSequenceDead(PcodeOp firstop, PcodeOp lastop, PcodeOp prev)
        {
            list<PcodeOp*>::iterator enditer = lastop.insertiter;
            ++enditer;
            list<PcodeOp*>::iterator previter = prev.insertiter;
            ++previter;
            if (previter != firstop.insertiter) // Check for degenerate move
                deadlist.splice(previter, deadlist, firstop.insertiter, enditer);
        }

        /// Mark any COPY ops in the given range as \e incidental
        /// Incidental COPYs are not considered active use of parameter passing Varnodes by
        /// parameter analysis algorithms.
        /// \param firstop is the start of the range of incidental COPY ops
        /// \param lastop is the end of the range of incidental COPY ops
        public void markIncidentalCopy(PcodeOp firstop, PcodeOp lastop)
        {
            list<PcodeOp*>::iterator iter = firstop.insertiter;
            list<PcodeOp*>::iterator enditer = lastop.insertiter;
            ++enditer;
            while (iter != enditer)
            {
                PcodeOp* op = *iter;
                ++iter;
                if (op.code() == CPUI_COPY)
                    op.setAdditionalFlag(PcodeOp::incidental_copy);
            }
        }

        /// Return \b true if there are no PcodeOps in \b this container
        public bool empty() => optree.empty();

        /// Find the first executing PcodeOp for a target address
        /// Find the first PcodeOp at or after the given Address assuming they have not
        /// yet been broken up into basic blocks. Take into account delay slots.
        /// \param addr is the given Address
        /// \return the targeted PcodeOp (or NULL)
        public PcodeOp target(Address addr)
        {
            PcodeOpTree::const_iterator iter = optree.lower_bound(SeqNum(addr, 0));
            if (iter == optree.end()) return (PcodeOp)null;
            return (*iter).second.target();
        }

        /// Find a PcodeOp by sequence number
        /// \param num is the given sequence number
        /// \return the matching PcodeOp (or NULL)
        public PcodeOp findOp(SeqNum num)
        {
            PcodeOpTree::const_iterator iter = optree.find(num);
            if (iter == optree.end()) return (PcodeOp)null;
            return (*iter).second;
        }

        /// Find the PcodeOp considered a \e fallthru of the given PcodeOp
        /// The term \e fallthru in this context refers to p-code \e not assembly instructions.
        /// \param op is the given PcodeOp
        /// \return the fallthru PcodeOp
        public PcodeOp fallthru(PcodeOp op)
        {
            PcodeOp* retop;
            if (op.isDead())
            {
                // In this case we know an instruction is contiguous
                // in the dead list
                list<PcodeOp*>::const_iterator iter = op.insertiter;
                ++iter;
                if (iter != deadlist.end())
                {
                    retop = *iter;
                    if (!retop.isInstructionStart()) // If the next in dead list is not marked
                        return retop;       // It is in the same instruction, and is the fallthru
                }
                --iter;
                SeqNum max = op.getSeqNum();
                while (!(*iter).isInstructionStart()) // Find start of instruction
                    --iter;
                // Find biggest sequence number in this instruction
                // This is probably -op- itself because it is the
                // last op in the instruction, but it might not be
                // because of delay slot reordering
                while ((iter != deadlist.end()) && (*iter != op))
                {
                    if (max < (*iter).getSeqNum())
                        max = (*iter).getSeqNum();
                    ++iter;
                }
                PcodeOpTree::const_iterator nextiter = optree.upper_bound(max);
                if (nextiter == optree.end()) return (PcodeOp)null;
                retop = (*nextiter).second;
                return retop;
            }
            else
                return op.nextOp();
        }

        /// \brief Start of all PcodeOps in sequence number order
        public PcodeOpTree::const_iterator beginAll() => optree.begin();

        /// \brief End of all PcodeOps in sequence number order
        public PcodeOpTree::const_iterator endAll() => optree.end();

        /// \brief Start of all PcodeOps at one Address
        public PcodeOpTree::const_iterator begin(Address addr)
        {
            return optree.lower_bound(SeqNum(addr, 0));
        }

        /// \brief End of all PcodeOps at one Address
        public PcodeOpTree::const_iterator end(Address addr)
        {
            return optree.upper_bound(SeqNum(addr, uint.MaxValue));
        }

        /// \brief Start of all PcodeOps marked as \e alive
        public List<PcodeOp>::const_iterator beginAlive() => alivelist.begin();

        /// \brief End of all PcodeOps marked as \e alive
        public List<PcodeOp*>::const_iterator endAlive() => alivelist.end();

        /// \brief Start of all PcodeOps marked as \e dead
        public List<PcodeOp>::const_iterator beginDead() => deadlist.begin();

        /// \brief End of all PcodeOps marked as \e dead
        public List<PcodeOp>::const_iterator endDead() => deadlist.end();

        /// \brief Start of all PcodeOps sharing the given op-code
        public List<PcodeOp>::const_iterator begin(OpCode opc)
        {
            switch (opc)
            {
                case CPUI_STORE:
                    return storelist.begin();
                case CPUI_LOAD:
                    return loadlist.begin();
                case CPUI_RETURN:
                    return returnlist.begin();
                case CPUI_CALLOTHER:
                    return useroplist.begin();
                default:
                    break;
            }
            return alivelist.end();
        }

        /// \brief End of all PcodeOps sharing the given op-code
        public List<PcodeOp>::const_iterator end(OpCode opc)
        {
            switch (opc)
            {
                case CPUI_STORE:
                    return storelist.end();
                case CPUI_LOAD:
                    return loadlist.end();
                case CPUI_RETURN:
                    return returnlist.end();
                case CPUI_CALLOTHER:
                    return useroplist.end();
                default:
                    break;
            }
            return alivelist.end();
        }
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
        internal static int functionalEqualityLevel(Varnode vn1, Varnode vn2,
            out Varnode res1, out Varnode res2)

        {
            int testval = functionalEqualityLevel0(vn1, vn2);
            if (testval != 1) return testval;
            PcodeOp* op1 = vn1.getDef();
            PcodeOp* op2 = vn2.getDef();
            OpCode opc = op1.code();

            if (opc != op2.code()) return -1;

            int num = op1.numInput();
            if (num != op2.numInput()) return -1;
            if (op1.isMarker()) return -1;
            if (op2.isCall()) return -1;
            if (opc == CPUI_LOAD)
            {
                // FIXME: We assume two loads produce the same
                // result if the address is the same and the loads
                // occur in the same instruction
                if (op1.getAddr() != op2.getAddr()) return -1;
            }
            if (num >= 3)
            {
                if (opc != CPUI_PTRADD) return -1; // If this is a PTRADD
                if (op1.getIn(2).getOffset() != op2.getIn(2).getOffset()) return -1; // Make sure the elsize constant is equal
                num = 2;            // Otherwise treat as having 2 inputs
            }
            for (int i = 0; i < num; ++i)
            {
                res1[i] = op1.getIn(i);
                res2[i] = op2.getIn(i);
            }

            testval = functionalEqualityLevel0(res1[0], res2[0]);
            if (testval == 0)
            {           // A match locks in this comparison ordering
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
            if (testval2 == 0)
            {       // A match locks in this comparison ordering
                return testval;
            }
            int unmatchsize;
            if ((testval == 1) && (testval2 == 1))
                unmatchsize = 2;
            else
                unmatchsize = -1;

            if (!op1.isCommutative()) return unmatchsize;
            // unmatchsize must be 2 or -1 here on a commutative operator,
            // try flipping
            int comm1 = functionalEqualityLevel0(res1[0], res2[1]);
            int comm2 = functionalEqualityLevel0(res1[1], res2[0]);
            if ((comm1 == 0) && (comm2 == 0))
                return 0;
            if ((comm1 < 0) || (comm2 < 0))
                return unmatchsize;
            if (comm1 == 0)
            {       // AND (comm2==1)
                res1[0] = res1[1];      // Left over unmatch is res1[1] and res2[0]
                return 1;
            }
            if (comm2 == 0)
            {       // AND (comm1==1)
                res2[0] = res2[1];      // Left over unmatch is res1[0] and res2[1]
                return 1;
            }
            // If we reach here (comm1==1) AND (comm2==1)
            if (unmatchsize == 2)       // If the original ordering wasn't impossible
                return 2;           // Prefer the original ordering
            Varnode* tmpvn = res2[0];   // Otherwise swap the ordering
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
            Varnode* buf1[2];
            Varnode* buf2[2];
            return (functionalEqualityLevel(vn1, vn2, buf1, buf2) == 0);
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
            PcodeOp* op1,*op2;
            int i, num;

            if (vn1 == vn2) return false;
            if ((!vn1.isWritten()) || (!vn2.isWritten()))
            {
                if (vn1.isConstant() && vn2.isConstant())
                    return !(vn1.getAddr() == vn2.getAddr());
                if (vn1.isInput() && vn2.isInput()) return false; // Might be the same
                if (vn1.isFree() || vn2.isFree()) return false; // Might be the same
                return true;
            }
            op1 = vn1.getDef();
            op2 = vn2.getDef();
            if (op1.code() != op2.code()) return true;
            num = op1.numInput();
            if (num != op2.numInput()) return true;
            if (depth == 0) return true;    // Different as far as we can tell
            depth -= 1;
            for (i = 0; i < num; ++i)
                if (functionalDifference(op1.getIn(i), op2.getIn(i), depth))
                    return true;
            return false;
        }
    }
}
