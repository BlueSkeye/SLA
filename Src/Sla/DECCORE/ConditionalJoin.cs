using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Discover and eliminate \e split conditions
    /// A \b split condition is when a conditional expression, resulting in a CBRANCH,
    /// is duplicated across two blocks that would otherwise merge.
    /// Instead of a single conditional in a merged block,
    /// there are two copies of the conditional, two splitting blocks and no direct merge.
    internal class ConditionalJoin
    {
        /// \brief A pair of Varnode objects that have been split (and should be merged)
        internal struct MergePair
        {
            internal Varnode side1;     ///< Varnode coming from block1
            internal Varnode side2;     ///< Varnode coming from block2

            /// Construct from Varnode objects
            internal MergePair(Varnode s1, Varnode s2)
            {
                side1 = s1;
                side2 = s2;
            }

            /// Lexicographic comparator
            /// Compare based on the creation index of \b side1 first then \b side2
            /// \param op2 is the MergePair to compare to \b this
            /// \return \b true if \b this comes before \b op2
            public static bool operator <(MergePair op1, MergePair op2)
            {
                uint s1 = op1.side1.getCreateIndex();
                uint s2 = op2.side1.getCreateIndex();
                return (s1 != s2) ? (s1 < s2) : (op1.side2.getCreateIndex() < op2.side2.getCreateIndex());
            }

            public static bool operator >(MergePair op1, MergePair op2)
            {
                uint s1 = op1.side1.getCreateIndex();
                uint s2 = op2.side1.getCreateIndex();
                return (s1 != s2) ? (s1 > s2) : (op1.side2.getCreateIndex() > op2.side2.getCreateIndex());
            }
        }

        /// The function being analyzed
        private Funcdata data;
        /// Side 1 of the (putative) split
        private BlockBasic block1;
        /// Side 2 of the (putative) split
        private BlockBasic block2;
        /// First (common) exit point
        private BlockBasic exita;
        /// Second (common) exit point
        private BlockBasic exitb;
        /// In edge of \b exita coming from \b block1
        private int a_in1;
        /// In edge of \b exita coming from \b block2
        private int a_in2;
        /// In edge of \b exitb coming from \b block1
        private int b_in1;
        /// In edge of \b exitb coming from \b block2
        private int b_in2;
        /// CBRANCH at bottom of \b block1
        private PcodeOp cbranch1;
        /// CBRANCH at bottom of \b block2
        private PcodeOp cbranch2;
        /// The new joined condition block
        private BlockBasic joinblock;
        /// Map from the MergePair of Varnodes to the merged Varnode
        private Dictionary<MergePair, Varnode?> mergeneed;

        /// Search for duplicate conditional expressions
        /// Given two conditional blocks, determine if the corresponding conditional
        /// expressions are equivalent, up to Varnodes that need to be merged.
        /// Any Varnode pairs that need to be merged are put in the \b mergeneed map.
        /// \return \b true if there are matching conditions
        private bool findDups()
        {
            cbranch1 = block1.lastOp();
            if (cbranch1.code() != OpCode.CPUI_CBRANCH) {
                return false;
            }
            cbranch2 = block2.lastOp();
            if (cbranch2.code() != OpCode.CPUI_CBRANCH) {
                return false;
            }

            if (cbranch1.isBooleanFlip()) {
                // flip hasn't propagated through yet
                return false;
            }
            if (cbranch2.isBooleanFlip()) {
                return false;
            }

            Varnode vn1 = cbranch1.getIn(1);
            Varnode vn2 = cbranch2.getIn(1);

            if (vn1 == vn2) {
                return true;
            }

            // Parallel RulePushMulti,  so we know it will apply if we do the join
            if (!vn1.isWritten()) {
                return false;
            }
            if (!vn2.isWritten()) {
                return false;
            }
            if (vn1.isSpacebase()) {
                return false;
            }
            if (vn2.isSpacebase()) {
                return false;
            }
            Varnode[] buf1 = new Varnode[2];
            Varnode[] buf2 = new Varnode[2];
            int res = PcodeOpBank.functionalEqualityLevel(vn1, vn2, buf1, buf2);
            if (res < 0) {
                return false;
            }
            if (res > 1) {
                return false;
            }
            PcodeOp op1 = vn1.getDef();
            if (op1.code() == OpCode.CPUI_SUBPIECE) {
                return false;
            }
            if (op1.code() == OpCode.CPUI_COPY) {
                return false;
            }
            mergeneed[new MergePair(vn1, vn2)] = null;
            return true;
        }

        /// \brief Look for additional Varnode pairs in an exit block that need to be merged.
        /// Varnodes that are merged in the exit block flowing from \b block1 and \b block2
        /// will need to merged in the new joined block.  Add these pairs to the \b mergeneed map.
        /// \param exit is the exit block
        /// \param in1 is the index of the edge coming from \b block1
        /// \param in2 is the index of the edge coming from \b block2
        private void checkExitBlock(BlockBasic exit, int in1, int in2)
        {
            LinkedListNode<PcodeOp>? iter = exit.beginOp();
            bool completed = (null != iter);

            while (!completed) {
                PcodeOp op = iter.Value;
                completed = (null != (iter = iter.Next));
                if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                    // Anything merging from our two root blocks -block1- and -block2-
                    Varnode vn1 = op.getIn(in1) ?? throw new ApplicationException();
                    Varnode vn2 = op.getIn(in2) ?? throw new ApplicationException();
                    if (vn1 != vn2) {
                        mergeneed[new MergePair(vn1, vn2)] = null;
                    }
                }
                else if (op.code() != OpCode.CPUI_COPY) {
                    break;
                }
            }
        }

        /// \brief Substitute new joined Varnode in the given exit block
        /// For any MULTIEQUAL in the \b exit, given two input slots, remove one Varnode,
        /// and substitute the other Varnode from the corresponding Varnode in the \b mergeneed map.
        /// \param exit is the exit block
        /// \param in1 is the index of the incoming edge from \b block1
        /// \param in2 is the index of the incoming edge from \b block2
        private void cutDownMultiequals(BlockBasic exit, int in1, int in2)
        {
            int lo, hi;
            if (in1 > in2) {
                hi = in1;
                lo = in2;
            }
            else {
                hi = in2;
                lo = in1;
            }
            LinkedListNode<PcodeOp>? iter = exit.beginOp();
            bool completed = (null != iter);
            while (!completed) {
                PcodeOp op = iter.Value;
                // Advance iterator before inserts happen
                completed = (null != (iter = iter.Next));
                if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                    Varnode vn1 = op.getIn(in1) ?? throw new ApplicationException();
                    Varnode vn2 = op.getIn(in2) ?? throw new ApplicationException();
                    if (vn1 == vn2) {
                        data.opRemoveInput(op, hi);
                    }
                    else {
                        Varnode subvn = mergeneed[new MergePair(vn1, vn2)]
                            ?? throw new ApplicationException();
                        data.opRemoveInput(op, hi);
                        data.opSetInput(op, subvn, lo);
                    }
                    if (op.numInput() == 1) {
                        data.opUninsert(op);
                        data.opSetOpcode(op, OpCode.CPUI_COPY);
                        data.opInsertBegin(op, exit);
                    }
                }
                else if (op.code() != OpCode.CPUI_COPY) {
                    break;
                }
            }
        }

        /// Join the Varnodes in the new \b joinblock
        /// Create a new Varnode and its defining MULTIEQUAL operation
        /// for each MergePair in the map.
        private void setupMultiequals()
        {
            foreach (KeyValuePair<MergePair, Varnode>  iter in mergeneed) {
                if (iter.Value != null) {
                    continue;
                }
                Varnode vn1 = iter.Key.side1;
                Varnode vn2 = iter.Key.side2;
                PcodeOp multi = data.newOp(2, cbranch1.getAddr());
                data.opSetOpcode(multi, OpCode.CPUI_MULTIEQUAL);
                Varnode outvn = data.newUniqueOut(vn1.getSize(), multi);
                data.opSetInput(multi, vn1, 0);
                data.opSetInput(multi, vn2, 1);
                mergeneed[iter.Key] = outvn;
                data.opInsertEnd(multi, joinblock);
            }
        }

        // Move one of the duplicated CBRANCHs into the new \b joinblock
        /// Remove the other CBRANCH
        private void moveCbranch()
        {
            Varnode vn1 = cbranch1.getIn(1);
            Varnode vn2 = cbranch2.getIn(1);
            data.opUninsert(cbranch1);
            data.opInsertEnd(cbranch1, joinblock);
            Varnode vn = (vn1 != vn2) ? mergeneed[new MergePair(vn1, vn2)] : vn1;
            data.opSetInput(cbranch1, vn, 1);
            data.opDestroy(cbranch2);
        }

        /// Constructor
        public ConditionalJoin(Funcdata fd)
        {
            data = fd;
        }

        /// Test blocks for the merge condition
        /// Given a pair of conditional blocks, make sure that they match the \e split conditions
        /// necessary for merging and set up to do the merge.
        /// If the conditions are not met, this method cleans up so that additional calls can be made.
        /// \param b1 is the BlockBasic exhibiting one side of the split
        /// \param b2 is the BlockBasic on the other side of the split
        /// \return \b true if the conditions for merging are met
        public bool match(BlockBasic b1, BlockBasic b2)
        {
            block1 = b1;
            block2 = b2;
            // Check for the ConditionalJoin block pattern
            if (block2 == block1) {
                return false;
            }
            if (block1.sizeOut() != 2) {
                return false;
            }
            if (block2.sizeOut() != 2) {
                return false;
            }
            exita = (BlockBasic)block1.getOut(0);
            exitb = (BlockBasic)block1.getOut(1);
            if (exita == exitb) {
                return false;
            }
            if (block2.getOut(0) == exita) {
                if (block2.getOut(1) != exitb) {
                    return false;
                }
                a_in2 = block2.getOutRevIndex(0);
                b_in2 = block2.getOutRevIndex(1);
            }
            else if (block2.getOut(0) == exitb) {
                if (block2.getOut(1) != exita) {
                    return false;
                }
                a_in2 = block2.getOutRevIndex(1);
                b_in2 = block2.getOutRevIndex(0);
            }
            else{
                return false;
            }
            a_in1 = block1.getOutRevIndex(0);
            b_in1 = block1.getOutRevIndex(1);

            if (!findDups()) {
                clear();
                return false;
            }
            checkExitBlock(exita, a_in1, a_in2);
            checkExitBlock(exitb, b_in1, b_in2);
            return true;
        }

        /// Execute the merge
        /// All the conditions have been met.  Go ahead and do the join.
        public void execute()
        {
            joinblock = data.nodeJoinCreateBlock(block1, block2, exita, exitb, (a_in1 > a_in2),
                (b_in1 > b_in2), cbranch1.getAddr());
            setupMultiequals();
            moveCbranch();
            cutDownMultiequals(exita, a_in1, a_in2);
            cutDownMultiequals(exitb, b_in1, b_in2);
        }

        /// Clear for a new test
        public void clear()
        {
            // Clear out data from previous join
            mergeneed.Clear();
        }
    }
}
