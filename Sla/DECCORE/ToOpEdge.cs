using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief An edge between a Varnode and a PcodeOp
    /// A DynamicHash is defined on a sub-graph of the data-flow, and this defines an edge
    /// in the sub-graph.  The edge can either be from an input Varnode to the PcodeOp
    /// that reads it, or from a PcodeOp to the Varnode it defines.
    internal class ToOpEdge
    {
        /// The PcodeOp defining the edge
        private static readonly PcodeOp op;
        /// Slot containing the input Varnode or -1 for the p-code op output
        private int slot;
        
        public ToOpEdge(PcodeOp o, int4 s)
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
            const Address &addr1(op->getSeqNum().getAddr());
            const Address &addr2(op2.op->getSeqNum().getAddr());
            if (addr1 != addr2)
                return (addr1 < addr2);
            uintm ord1 = op->getSeqNum().getOrder();
            uintm ord2 = op2.op->getSeqNum().getOrder();
            if (ord1 != ord2)
                return (ord1 < ord2);
            return (slot < op2.slot);
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
            reg = crc_update(reg, (uint4)slot);
            reg = crc_update(reg, DynamicHash::transtable[op->code()]);
            uintb val = op->getSeqNum().getAddr().getOffset();
            int4 sz = op->getSeqNum().getAddr().getAddrSize();
            for (int4 i = 0; i < sz; ++i)
            {
                reg = crc_update(reg, (uint4)val); // Hash in the address
                val >>= 8;
            }
            return reg;
        }
    }
}
