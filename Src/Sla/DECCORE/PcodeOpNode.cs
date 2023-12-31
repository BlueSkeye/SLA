﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An edge in a data-flow path or graph
    ///
    /// A minimal node for traversing expressions in the data-flow
    internal struct PcodeOpNode
    {
        /// The p-code end-point of the edge
        internal PcodeOp? op;
        /// Slot indicating the input Varnode end-point of the edge
        internal int slot;

        public PcodeOpNode()
        {
            op = (PcodeOp)null;
            slot = 0;
        }   ///< Unused constructor

        internal PcodeOpNode(PcodeOp o, int s)
        {
            op = o;
            slot = s;
        }

        /// Simple comparator for putting edges in a sorted container
        public static bool operator <(PcodeOpNode op1, PcodeOpNode op2)
        {
            if (op1.op != op2.op)
                return (op1.op.getSeqNum().getTime() < op2.op.getSeqNum().getTime());
            if (op1.slot != op2.slot)
                return (op1.slot < op2.slot);
            return false;
        }

        public static bool operator >(PcodeOpNode op1, PcodeOpNode op2)
        {
            if (op1.op != op2.op)
                return (op1.op.getSeqNum().getTime() > op2.op.getSeqNum().getTime());
            if (op1.slot != op2.slot)
                return (op1.slot > op2.slot);
            return false;
        }
    }
}
