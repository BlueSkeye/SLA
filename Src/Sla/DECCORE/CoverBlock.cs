using Sla.CORE;
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
            if (object.ReferenceEquals(op, null)) {
                // Special marker for very beginning of block
                return (uint)0;
            }
            if (object.ReferenceEquals(op, PcodeOp.ONE)) {
                // Special marker for very end of block
                return uint.MaxValue;
            }
            if (object.ReferenceEquals(op, PcodeOp.TWO)) {
                // Special marker for input
                return (uint)0;
            }
            if (op.isMarker()) {
                if (op.code() == OpCode.CPUI_MULTIEQUAL)
                    // MULTIEQUALs are considered very beginning
                    return (uint)0;
                else if (op.code() == OpCode.CPUI_INDIRECT)
                    // INDIRECTs are considered to be at the location of the op they are indirect for
                    return PcodeOp.getOpFromConst(op.getIn(1).getAddr()).getSeqNum().getOrder();
            }
            return op.getSeqNum().getOrder();
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
            start = null;
            stop = PcodeOp.ONE;
        }

        /// Reset start of range
        public void setBegin(PcodeOp begin)
        {
            start = begin;
            if (stop == null) {
                stop = PcodeOp.ONE;
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
            uint ustart, ustop;
            uint u2start, u2stop;

            if (empty()) return 0;
            if (op2.empty()) return 0;

            ustart = (uint)getUIndex(start);
            ustop = (uint)getUIndex(stop);
            u2start = (uint)getUIndex(op2.start);
            u2stop = (uint)getUIndex(op2.stop);
            if (ustart <= ustop) {
                if (u2start <= u2stop) {
                    // We are both one piece
                    if ((ustop <= u2start) || (u2stop <= ustart)) {
                        if ((ustart == u2stop) || (ustop == u2start))
                            // Boundary intersection
                            return 1;
                        else
                            // No intersection
                            return 0;
                    }
                }
                else {
                    // They are two-piece, we are one-piece
                    if ((ustart >= u2stop) && (ustop <= u2start)) {
                        if ((ustart == u2stop) || (ustop == u2start))
                            return 1;
                        else
                            return 0;
                    }
                }
            }
            else {
                if (u2start <= u2stop) {
                    // They are one piece, we are two-piece
                    if ((u2start >= ustop) && (u2stop <= ustart)) {
                        if ((u2start == ustop) || (u2stop == ustart))
                            return 1;
                        else
                            return 0;
                    }
                }
                // If both are two-pieces, then the intersection must be an interval
            }
            // Interval intersection
            return 2;
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
            uint ustart, ustop, upoint;

            if (empty()) return false;
            upoint = (uint)getUIndex(point);
            ustart = (uint)getUIndex(start);
            ustop = (uint)getUIndex(stop);

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
            uint val;

            if (empty()) return 0;
            val = (uint)getUIndex(point);
            if (getUIndex(start) == val) {
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
            uint ustart, u2start;

            // Nothing to merge in
            if (op2.empty()) return;
            if (empty()) {
                start = op2.start;
                stop = op2.stop;
                return;
            }
            ustart = (uint)getUIndex(start);
            u2start = (uint)getUIndex(op2.start);
            // Is start contained in op2
            internal4 = ((ustart == (uint)0) && (op2.stop == PcodeOp.ONE));
            internal1 = internal4 || op2.contain(start);
            // Is op2.start contained in this
            internal3 = ((u2start == 0) && (stop == PcodeOp.ONE));
            internal2 = internal3 || contain(op2.start);

            if (internal1 && internal2)
                if ((ustart != u2start) || internal3 || internal4) {
                    // Covered entire block
                    setAll();
                    return;
                }
            if (internal1)
                // Pick non-internal start
                start = op2.start;
            else if ((!internal1) && (!internal2)) {
                // Disjoint intervals
                // Pick earliest start
                if (ustart < u2start)
                    // then take other stop
                    stop = op2.stop;
                else
                    start = op2.start;
                return;
            }
            // Pick non-internal stop
            if (internal3 || op2.contain(stop))
                stop = op2.stop;
        }

        /// Dump a description to stream
        /// Print a description of the covered range of ops in this block
        /// \param s is the output stream
        public void print(TextWriter s)
        {
            if (empty()) {
                s.Write("empty");
                return;
            }
            uint ustart = (uint)getUIndex(start);
            uint ustop = (uint)getUIndex(stop);
            if (ustart == (uint)0)
                s.Write("begin");
            else if (ustart == uint.MaxValue)
                s.Write("end");
            else
                s.Write(start.getSeqNum());

            s.Write('-');

            if (ustop == (uint)0)
                s.Write("begin");
            else if (ustop == uint.MaxValue)
                s.Write("end");
            else
                s.Write(stop.getSeqNum());
        }
    }
}
