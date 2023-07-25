using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A (putative) switch variable Varnode and a constraint imposed by a CBRANCH
    ///
    /// The record constrains a specific Varnode.  If the associated CBRANCH is followed
    /// along the path that reaches the switch's BRANCHIND, then we have an explicit
    /// description of the possible values the Varnode can hold.
    internal class GuardRecord
    {
        /// PcodeOp CBRANCH the branches around the switch
        private PcodeOp cbranch;
        /// The immediate PcodeOp causing the restriction
        private PcodeOp readOp;
        /// The Varnode being restricted
        private Varnode vn;
        /// Value being (quasi)copied to the Varnode
        private Varnode baseVn;
        /// Specific CBRANCH path going to the switch
        private int4 indpath;
        /// Number of bits copied (all other bits are zero)
        private int4 bitsPreserved;
        /// Range of values causing the CBRANCH to take the path to the switch
        private CircleRange range;
        /// \b true if guarding CBRANCH is duplicated across multiple blocks
        private bool unrolled;

        /// \param bOp is the CBRANCH \e guarding the switch
        /// \param rOp is the PcodeOp immediately reading the Varnode
        /// \param path is the specific branch to take from the CBRANCH to reach the switch
        /// \param rng is the range of values causing the switch path to be taken
        /// \param v is the Varnode holding the value controlling the CBRANCH
        /// \param unr is \b true if the guard is duplicated across multiple blocks
        public GuardRecord(PcodeOp bOp, PcodeOp rOp, int4 path, CircleRange rng,Varnode v,
            bool unr = false)
        {
            cbranch = bOp;
            readOp = rOp;
            indpath = path;
            range = rng;
            vn = v;
            baseVn = quasiCopy(v, bitsPreserved);       // Look for varnode whose bits are copied
            unrolled = unr;
        }

        /// Is \b this guard duplicated across multiple blocks
        public bool isUnrolled() => unrolled;

        /// Get the CBRANCH associated with \b this guard
        public PcodeOp getBranch() => cbranch;

        /// Get the PcodeOp immediately causing the restriction
        public PcodeOp getReadOp() => readOp;

        /// Get the specific path index going towards the switch
        public int4 getPath() => indpath;

        /// Get the range of values causing the switch path to be taken
        public CircleRange getRange() => range;

        /// Mark \b this guard as unused
        public void clear()
        {
            cbranch = (PcodeOp*)0;
        }

        /// \brief Determine if \b this guard applies to the given Varnode
        ///
        /// The guard applies if we know the given Varnode holds the same value as the Varnode
        /// attached to the guard. So we return:
        ///   - 0, if the two Varnodes do not clearly hold the same value.
        ///   - 1, if the two Varnodes clearly hold the same value.
        ///   - 2, if the two Varnode clearly hold the same value, pending no writes between their defining op.
        ///
        /// \param vn2 is the given Varnode being tested against \b this guard
        /// \param baseVn2 is the earliest Varnode from which the given Varnode is quasi-copied.
        /// \param bitsPreserved2 is the number of potentially non-zero bits in the given Varnode
        /// \return the matching code 0, 1, or 2
        public int4 valueMatch(Varnode vn2, Varnode baseVn2, int4 bitsPreserved2)
        {
            if (vn == vn2) return 1;        // Same varnode, same value
            PcodeOp* loadOp,*loadOp2;
            if (bitsPreserved == bitsPreserved2)
            {   // Are the same number of bits being copied
                if (baseVn == baseVn2)          // Are bits being copied from same varnode
                    return 1;                   // If so, values are the same
                loadOp = baseVn->getDef();          // Otherwise check if different base varnodes hold same value
                loadOp2 = baseVn2->getDef();
            }
            else
            {
                loadOp = vn->getDef();          // Check if different varnodes hold same value
                loadOp2 = vn2->getDef();
            }
            if (loadOp == (PcodeOp*)0) return 0;
            if (loadOp2 == (PcodeOp*)0) return 0;
            if (oneOffMatch(loadOp, loadOp2) == 1)      // Check for simple duplicate calculations
                return 1;
            if (loadOp->code() != CPUI_LOAD) return 0;
            if (loadOp2->code() != CPUI_LOAD) return 0;
            if (loadOp->getIn(0)->getOffset() != loadOp2->getIn(0)->getOffset()) return 0;
            Varnode* ptr = loadOp->getIn(1);
            Varnode* ptr2 = loadOp2->getIn(1);
            if (ptr == ptr2) return 2;
            if (!ptr->isWritten()) return 0;
            if (!ptr2->isWritten()) return 0;
            PcodeOp* addop = ptr->getDef();
            if (addop->code() != CPUI_INT_ADD) return 0;
            Varnode* constvn = addop->getIn(1);
            if (!constvn->isConstant()) return 0;
            PcodeOp* addop2 = ptr2->getDef();
            if (addop2->code() != CPUI_INT_ADD) return 0;
            Varnode* constvn2 = addop2->getIn(1);
            if (!constvn2->isConstant()) return 0;
            if (addop->getIn(0) != addop2->getIn(0)) return 0;
            if (constvn->getOffset() != constvn2->getOffset()) return 0;
            return 2;
        }

        /// \brief Return 1 if the two given PcodeOps produce exactly the same value, 0 if otherwise
        ///
        /// We up through only one level of PcodeOp calculation and only for certain binary ops
        /// where the second parameter is a constant.
        /// \param op1 is the first given PcodeOp to test
        /// \param op2 is the second given PcodeOp
        /// \return 1 if the same value is produced, 0 otherwise
        public static int4 oneOffMatch(PcodeOp op1, PcodeOp op2)
        {
            if (op1->code() != op2->code())
                return 0;
            switch (op1->code())
            {
                case CPUI_INT_AND:
                case CPUI_INT_ADD:
                case CPUI_INT_XOR:
                case CPUI_INT_OR:
                case CPUI_INT_LEFT:
                case CPUI_INT_RIGHT:
                case CPUI_INT_SRIGHT:
                case CPUI_INT_MULT:
                case CPUI_SUBPIECE:
                    if (op2->getIn(0) != op1->getIn(0)) return 0;
                    if (matching_constants(op2->getIn(1), op1->getIn(1)))
                        return 1;
                    break;
                default:
                    break;
            }
            return 0;
        }

        /// \brief Compute the source of a quasi-COPY chain for the given Varnode
        ///
        /// A value is a \b quasi-copy if a sequence of PcodeOps producing it always hold
        /// the value as the least significant bits of their output Varnode, but the sequence
        /// may put other non-zero values in the upper bits.
        /// This method computes the earliest ancestor Varnode for which the given Varnode
        /// can be viewed as a quasi-copy.
        /// \param vn is the given Varnode
        /// \param bitsPreserved will hold the number of least significant bits preserved by the sequence
        /// \return the earliest source of the quasi-copy, which may just be the given Varnode
        public static Varnode quasiCopy(Varnode vn, int4 bitsPreserved)
        {
            bitsPreserved = mostsigbit_set(vn->getNZMask()) + 1;
            if (bitsPreserved == 0) return vn;
            uintb mask = 1;
            mask <<= bitsPreserved;
            mask -= 1;
            PcodeOp* op = vn->getDef();
            Varnode* constVn;
            while (op != (PcodeOp*)0)
            {
                switch (op->code())
                {
                    case CPUI_COPY:
                        vn = op->getIn(0);
                        op = vn->getDef();
                        break;
                    case CPUI_INT_AND:
                        constVn = op->getIn(1);
                        if (constVn->isConstant() && constVn->getOffset() == mask)
                        {
                            vn = op->getIn(0);
                            op = vn->getDef();
                        }
                        else
                            op = (PcodeOp*)0;
                        break;
                    case CPUI_INT_OR:
                        constVn = op->getIn(1);
                        if (constVn->isConstant() && ((constVn->getOffset() | mask) == (constVn->getOffset() ^ mask)))
                        {
                            vn = op->getIn(0);
                            op = vn->getDef();
                        }
                        else
                            op = (PcodeOp*)0;
                        break;
                    case CPUI_INT_SEXT:
                    case CPUI_INT_ZEXT:
                        if (op->getIn(0)->getSize() * 8 >= bitsPreserved)
                        {
                            vn = op->getIn(0);
                            op = vn->getDef();
                        }
                        else
                            op = (PcodeOp*)0;
                        break;
                    case CPUI_PIECE:
                        if (op->getIn(1)->getSize() * 8 >= bitsPreserved)
                        {
                            vn = op->getIn(1);
                            op = vn->getDef();
                        }
                        else
                            op = (PcodeOp*)0;
                        break;
                    case CPUI_SUBPIECE:
                        constVn = op->getIn(1);
                        if (constVn->isConstant() && constVn->getOffset() == 0)
                        {
                            vn = op->getIn(0);
                            op = vn->getDef();
                        }
                        else
                            op = (PcodeOp*)0;
                        break;
                    default:
                        op = (PcodeOp*)0;
                        break;
                }
            }
            return vn;
        }
    }
}
