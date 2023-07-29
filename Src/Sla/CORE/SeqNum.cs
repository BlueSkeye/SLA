﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A class for uniquely labelling and comparing PcodeOps
    /// Different PcodeOps generated by a single machine instruction
    /// can only be labelled with a single Address. But PcodeOps
    /// must be distinguishable and compared for execution order.
    /// A SeqNum extends the address for a PcodeOp to include:
    ///   - A fixed \e time field, which is set at the time the PcodeOp
    ///     is created. The \e time field guarantees a unique SeqNum
    ///     for the life of the PcodeOp. 
    ///   - An \e order field, which is guaranteed to be comparable
    ///     for the execution order of the PcodeOp within its basic
    ///     block.  The \e order field also provides uniqueness but
    ///     may change over time if the syntax tree is manipulated.
    public class SeqNum
    {
        /// Program counter at start of instruction
        internal Address pc;
        /// Number to guarantee uniqueness
        internal uint uniq;
        /// Number for order comparisons within a block
        internal uint order;

        /// Create an invalid sequence number
        public SeqNum()
        {
        }

        /// Create an extremal sequence number
        public SeqNum(Address.mach_extreme ex)
        {
            pc = new Address(ex);
            uniq = (ex == Address.mach_extreme.m_minimal) ? 0 : uint.MaxValue;
        }

        /// Create a sequence number with a specific \e time field
        public SeqNum(Address a, uint b)
        {
            pc = a;
            uniq = b;
        }

        /// Copy a sequence number
        public SeqNum(SeqNum op2)
        {
            pc = op2.pc;
            uniq = op2.uniq;
        }

        /// Get the address portion of a sequence number
        internal Address getAddr()
        {
            return pc;
        }

        /// Get the \e time field of a sequence number
        internal uint getTime()
        {
            return uniq;
        }

        /// Get the \e order field of a sequence number
        internal uint getOrder()
        {
            return order;
        }

        /// Set the \e order field of a sequence number
        internal void setOrder(uint ord)
        {
            order = ord;
        }

        /// Compare two sequence numbers for equality
        public static bool operator ==(SeqNum op1, SeqNum op2)
        {
            return (op1.uniq == op2.uniq);
        }

        /// Compare two sequence numbers for inequality
        public static bool operator !=(SeqNum op1, SeqNum op2)
        {
            return (op1.uniq != op2.uniq);
        }

        /// Compare two sequence numbers with their natural order
        public static bool operator <(SeqNum op1, SeqNum op2)
        {
            return (op1.pc == op2.pc) ? (op1.uniq < op2.uniq) : (op1.pc < op2.pc);
        }

        public static bool operator >(SeqNum op1, SeqNum op2)
        {
            return !(op1 < op2) && !(op1 == op2);
        }

        /// Encode a SeqNum to a stream
        internal void encode(ref Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_SEQNUM);
            pc.getSpace().encodeAttributes(encoder, pc.getOffset());
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_UNIQ, uniq);
            encoder.closeElement(ElementId.ELEM_SEQNUM);
        }

        /// Decode a SeqNum from a stream
        internal static SeqNum decode(ref Decoder decoder)
        {
            uint uniq = ~((uint)0);
            uint elemId = decoder.openElement(ElementId.ELEM_SEQNUM);
            Address pc = Address.decode(decoder); // Recover address
            for (; ; )
            {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_UNIQ) {
                    uniq = (uint)decoder.readUnsignedInteger();
                    break;
                }
            }
            decoder.closeElement(elemId);
            return new SeqNum(pc, uniq);
        }

        ///// Write out a SeqNum in human readable form to a stream
        //friend ostream &operator<<(ostream &s,const SeqNum &sq);
    }
}