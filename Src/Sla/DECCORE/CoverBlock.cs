using Sla.DECCORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief The topological scope of a variable within a basic block
    /// Within a basic block, the topological scope of a variable can be considered
    /// a contiguous range of p-code operations. This range can be described with
    /// a \e start and \e stop PcodeOp object, indicating all p-code operations between
    /// the two inclusive.  The \e start and \e stop may hold special encodings meaning:
    ///   - From the beginning of the block
    ///   - To the end of the block
    internal class CoverBlock
    {
        /// Beginning of the range
        private PcodeOp? start;
        /// End of the range
        private PcodeOp? stop;

        /// Construct empty/uncovered block
        public CoverBlock()
        {
            start = null;
            stop = null;
        }

        /// Get the comparison index for a PcodeOp
        /// PcodeOp objects and a CoverBlock start/stop boundaries have
        /// a natural ordering that can be used to tell if a PcodeOp falls
        /// between boundary points and if CoverBlock objects intersect.
        /// Ordering is determined by comparing the values returned by this method.
        /// \param op is the PcodeOp and/or boundary point
        /// \return a value for comparison
        public static ulong getUIndex(PcodeOp op)
        {
            uintp switchval = (uintp)op;
            switch (switchval)
            {
                case 0:         // Special marker for very beginning of block
                    return (uintm)0;
                case 1:         // Special marker for very end of block
                    return ~((uintm)0);
                case 2:         // Special marker for input
                    return (uintm)0;
            }
            if (op->isMarker())
            {
                if (op->code() == CPUI_MULTIEQUAL) // MULTIEQUALs are considered very beginning
                    return (uintm)0;
                else if (op->code() == CPUI_INDIRECT) // INDIRECTs are considered to be at
                                                      // the location of the op they are indirect for
                    return PcodeOp::getOpFromConst(op->getIn(1)->getAddr())->getSeqNum().getOrder();
            }
            return op->getSeqNum().getOrder();
        }

        /// Get the start of the range
        public PcodeOp? getStart() => start;

        /// Get the end of the range
        public PcodeOp? getStop() => stop;

        /// Clear \b this block to empty/uncovered
        public void clear()
        {
            start = null;
            stop = null;
        }

        /// Mark whole block as covered
        public void setAll()
        {
            start = null; stop = (PcodeOp*)1;
        }

        /// Reset start of range
        public void setBegin(PcodeOp* begin)
        {
            start = begin;
            if (stop == null) {
                stop = (PcodeOp*)1;
            }
        }

        /// Reset end of range
        public void setEnd(PcodeOp end)
        {
            stop = end;
        }

        /// Compute intersection with another CoverBlock
        /// Characterize the intersection of \b this range with another CoverBlock.
        /// Return:
        ///   - 0 if there is no intersection
        ///   - 1 if only the intersection is at boundary points
        ///   - 2 if a whole interval intersects
        ///
        /// \param op2 is the other CoverBlock to compare
        /// \return the intersection characterization
        public int intersect(CoverBlock op2)
        {
            uintm ustart, ustop;
            uintm u2start, u2stop;

            if (empty()) return 0;
            if (op2.empty()) return 0;

            ustart = getUIndex(start);
            ustop = getUIndex(stop);
            u2start = getUIndex(op2.start);
            u2stop = getUIndex(op2.stop);
            if (ustart <= ustop)
            {
                if (u2start <= u2stop)
                { // We are both one piece
                    if ((ustop <= u2start) || (u2stop <= ustart))
                    {
                        if ((ustart == u2stop) || (ustop == u2start))
                            return 1;       // Boundary intersection
                        else
                            return 0;       // No intersection
                    }
                }
                else
                {           // They are two-piece, we are one-piece
                    if ((ustart >= u2stop) && (ustop <= u2start))
                    {
                        if ((ustart == u2stop) || (ustop == u2start))
                            return 1;
                        else
                            return 0;
                    }
                }
            }
            else
            {
                if (u2start <= u2stop)
                { // They are one piece, we are two-piece
                    if ((u2start >= ustop) && (u2stop <= ustart))
                    {
                        if ((u2start == ustop) || (u2stop == ustart))
                            return 1;
                        else
                            return 0;
                    }
                }
                // If both are two-pieces, then the intersection must be an interval
            }
            return 2;           // Interval intersection
        }

        /// Return \b true if \b this is empty/uncovered
        public bool empty()
        {
            return (start == null) && (stop == null);
        }

        /// Check containment of given point
        /// If the given PcodeOp or boundary point is contained in \b this range, return true.
        /// \param point is the given PcodeOp
        /// \return \b true if the point is contained
        public bool contain(PcodeOp point)
        {
            uintm ustart, ustop, upoint;

            if (empty()) return false;
            upoint = getUIndex(point);
            ustart = getUIndex(start);
            ustop = getUIndex(stop);

            if (ustart <= ustop)
                return ((upoint >= ustart) && (upoint <= ustop));
            return ((upoint <= ustop) || (upoint >= ustart));
        }

        /// Characterize given point as boundary
        /// Return:
        ///   - 0 if point not on boundary
        ///   - 1 if on tail
        ///   - 2 if on the defining point
        ///
        /// \param point is the given PcodeOp point
        /// \return the characterization
        public int boundary(PcodeOp point)
        {
            uintm val;

            if (empty()) return 0;
            val = getUIndex(point);
            if (getUIndex(start) == val)
            {
                if (start != null)
                    return 2;
            }
            if (getUIndex(stop) == val) return 1;
            return 0;
        }

        /// Merge another CoverBlock into \b this
        /// Compute the union of \b this with the other given CoverBlock,
        /// replacing \b this in place.
        /// \param op2 is the other given CoverBlock
        public void merge(CoverBlock op2)
        {
            bool internal1, internal2, internal3, internal4;
            uintm ustart, u2start;

            if (op2.empty()) return;    // Nothing to merge in
            if (empty())
            {
                start = op2.start;
                stop = op2.stop;
                return;
            }
            ustart = getUIndex(start);
            u2start = getUIndex(op2.start);
            // Is start contained in op2
            internal4 = ((ustart == (uintm)0) && (op2.stop == (const PcodeOp*)1));
            internal1 = internal4 || op2.contain(start);
            // Is op2.start contained in this
            internal3 = ((u2start == 0) && (stop == (const PcodeOp*)1));
            internal2 = internal3 || contain(op2.start);

            if (internal1 && internal2)
                if ((ustart != u2start) || internal3 || internal4)
                { // Covered entire block
                    setAll();
                    return;
                }
            if (internal1)
                start = op2.start;      // Pick non-internal start
            else if ((!internal1) && (!internal2))
            { // Disjoint intervals
                if (ustart < u2start)   // Pick earliest start
                    stop = op2.stop;        // then take other stop
                else
                    start = op2.start;
                return;
            }
            if (internal3 || op2.contain(stop)) // Pick non-internal stop
                stop = op2.stop;
        }

        /// Dump a description to stream
        /// Print a description of the covered range of ops in this block
        /// \param s is the output stream
        public void print(ostream s)
        {
            uintm ustart, ustop;

            if (empty())
            {
                s << "empty";
                return;
            }

            ustart = getUIndex(start);
            ustop = getUIndex(stop);
            if (ustart == (uintm)0)
                s << "begin";
            else if (ustart == ~((uintm)0))
                s << "end";
            else
                s << start->getSeqNum();

            s << '-';

            if (ustop == (uintm)0)
                s << "begin";
            else if (ustop == ~((uintm)0))
                s << "end";
            else
                s << stop->getSeqNum();
        }
    }
}
