using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An edge between a Varnode and a PcodeOp
    /// A DynamicHash is defined on a sub-graph of the data-flow, and this defines an edge
    /// in the sub-graph.  The edge can either be from an input Varnode to the PcodeOp
    /// that reads it, or from a PcodeOp to the Varnode it defines.
    internal class ToOpEdge
    {
        /// The PcodeOp defining the edge
        private readonly PcodeOp op;
        /// Slot containing the input Varnode or -1 for the p-code op output
        private int slot;

        internal static IComparer<ToOpEdge> Comparer => EdgeComparer.Singleton;

        private class EdgeComparer : IComparer<ToOpEdge>
        {
            internal static EdgeComparer Singleton = new EdgeComparer();

            private EdgeComparer()
            {
            }
            
            public int Compare(ToOpEdge? x, ToOpEdge? y)
            {
                if (null == x) return -1;
                if (null == y) return -1;
                Address addr1 = x.op.getSeqNum().getAddr();
                Address addr2 = y.op.getSeqNum().getAddr();
                if (addr1 != addr2)
                    return (addr1 < addr2) ? -1 : 1;
                uint ord1 = x.op.getSeqNum().getOrder();
                uint ord2 = y.op.getSeqNum().getOrder();
                if (ord1 == ord2) {
                    if (x.slot == y.slot) return 0;
                    return (x.slot < y.slot) ? -1 : 1;
                }
                return (ord1 < ord2) ? -1 : 1;
            }
        }

        public ToOpEdge(PcodeOp o, int s)
        {
            op = o;
            slot = s;
        }

        /// Get the PcodeOp defining the edge
        public PcodeOp getOp() => op;

        /// Get the slot of the starting Varnode
        public int getSlot() => slot;

        /// Compare two edges based on PcodeOp
        /// These edges are sorted to provide consistency to the hash
        /// The sort is based on the PcodeOp sequence number first, then the Varnode slot
        /// \param op2 is the edge to compare \b this to
        /// \return \b true if \b this should be ordered before the other edge
        public static bool operator <(ToOpEdge op1, ToOpEdge op2)
        {
            return 0 > Comparer.Compare(op1, op2);
        }

        public static bool operator >(ToOpEdge op1, ToOpEdge op2)
        {
            return 0 < Comparer.Compare(op1, op2);
        }

        /// Hash \b this edge into an accumulator
        /// The hash accumulates:
        ///   - the Varnode slot
        ///   - the address of the PcodeOp
        ///   - the op-code of the PcodeOp
        ///
        /// The op-codes are translated so that the hash is invariant under
        /// common variants.
        /// \param reg is the incoming hash accumulator value
        /// \return the accumulator value with \b this edge folded in
        public uint hash(uint reg)
        {
            reg = Globals.crc_update(reg, (uint)slot);
            reg = Globals.crc_update(reg, DynamicHash.transtable[(int)op.code()]);
            ulong val = op.getSeqNum().getAddr().getOffset();
            int sz = op.getSeqNum().getAddr().getAddrSize();
            for (int i = 0; i < sz; ++i) {
                reg = Globals.crc_update(reg, (uint)val); // Hash in the address
                val >>= 8;
            }
            return reg;
        }
    }
}
