using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A contiguous range of bytes in some address space
    public class Range : IComparable<Range>
    {
        // friend class RangeList;
        /// Space containing range
        internal AddrSpace? spc;
        /// Offset of first byte in \b this Range
        internal ulong first;
        /// Offset of last byte in \b this Range
        internal ulong last;

        /// \brief Construct a Range from offsets
        /// Offsets must expressed in \e bytes as opposed to addressable \e words
        /// \param s is the address space containing the range
        /// \param f is the offset of the first byte in the range
        /// \param l is the offset of the last byte in the range
        public Range(AddrSpace s, ulong f, ulong l)
        {
            spc = s;
            first = f;
            last = l;
        }

        /// Constructor for use with decode
        public Range()
        {
        }

        /// Construct range out of basic properties
        public Range(RangeProperties properties, AddrSpaceManager manage)
        {
            if (properties.isRegister) {
                Translate trans = manage.getDefaultCodeSpace().getTrans();
                VarnodeData point = trans.getRegister(properties.spaceName);
                spc = point.space;
                first = point.offset;
                last = (first - 1) + point.size;
                return;
            }
            spc = manage.getSpaceByName(properties.spaceName);
            if (spc == null)
                throw new LowlevelError($"Undefined space: {properties.spaceName}");

            if (spc == null) {
                throw new LowlevelError("No address space indicated in range tag");
            }
            first = properties.first;
            last = properties.last;
            if (!properties.seenLast) {
                last = spc.getHighest();
            }
            if (first > spc.getHighest() || last > spc.getHighest() || last < first) {
                throw new LowlevelError("Illegal range tag");
            }
        }

        /// Get the address space containing \b this Range
        public AddrSpace getSpace()
        {
            return spc;
        }

        /// Get the offset of the first byte in \b this Range
        public ulong getFirst()
        {
            return first;
        }

        ///< Get the offset of the last byte in \b this Range
        public ulong getLast()
        {
            return last;
        }

        ///< Get the address of the first byte
        public Address getFirstAddr()
        {
            return new Address(spc, first);
        }

        ///< Get the address of the last byte
        public Address getLastAddr()
        {
            return new Address(spc, last);
        }

        ///< Get address of first byte after \b this
        /// Get the last address +1, updating the space, or returning
        /// the extremal address if necessary
        /// \param manage is used to fetch the next address space
        public Address getLastAddrOpen(AddrSpaceManager manage)
        {
            AddrSpace curspc = spc;
            ulong curlast = last;
            if (curlast == curspc.getHighest()) {
                curspc = manage.getNextSpaceInOrder(curspc);
                curlast = 0;
            }
            else {
                curlast += 1;
            }
            return (curspc == null) 
                ? new Address(Address.mach_extreme.m_maximal)
                : new Address(curspc, curlast);
        }

        ///< Determine if the address is in \b this Range
        /// \brief Sorting operator for Ranges
        /// Compare based on address space, then the starting offset
        /// \param op2 is the Range to compare with \b this
        /// \return \b true if \b this comes before op2
        /// \param addr is the Address to test for containment
        /// \return \b true if addr is in \b this Range
        public bool contains(Address addr)
        {
            return (spc == addr.getSpace())
                && (first <= addr.getOffset())
                && (last >= addr.getOffset());
        }

        public static bool operator <(Range op1, Range op2)
        {
            return (op1.spc.getIndex() != op2.spc.getIndex()) 
                ? (op1.spc.getIndex() < op2.spc.getIndex())
                : (op1.first < op2.first);
        }

        public static bool operator >(Range op1, Range op2)
        {
            return !(op1 < op2) && !(op1 == op2);
        }

        public static bool operator==(Range op1, Range op2)
        {
            return (op1.spc.getIndex() == op2.spc.getIndex())
                && (op1.first == op2.first);
        }

        public static bool operator !=(Range op1, Range op2)
        {
            return !(op1 == op2);
        }

        ///< Print \b this Range to a stream
        /// Output a description of this Range like:  ram: 7f-9c
        /// \param s is the output stream
        public void printBounds(TextWriter s)
        {
            s.Write($"{spc.getName()}: {first:X}-{last:X}");
        }

        ///< Encode \b this Range to a stream
        /// Encode \b this to a stream as a \<range> element.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_RANGE);
            encoder.writeSpace(AttributeId.ATTRIB_SPACE, spc);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_FIRST, first);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_LAST, last);
            encoder.closeElement(ElementId.ELEM_RANGE);
        }

        ///< Restore \b this from a stream
        /// Reconstruct this object from a \<range> or \<register> element
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement();
            if ((elemId != ElementId.ELEM_RANGE) && (elemId != ElementId.ELEM_REGISTER)) {
                throw new DecoderError("Expecting <range> or <register> element");
            }
            decodeFromAttributes(decoder);
            decoder.closeElement(elemId);
        }

        /// Read \b from attributes on another tag
        /// Reconstruct from attributes that may not be part of a \<range> element.
        /// \param decoder is the stream decoder
        public void decodeFromAttributes(Sla.CORE.Decoder decoder)
        {
            spc = null;
            bool seenLast = false;
            first = 0;
            last = 0;
            while(true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) {
                    break;
                }
                if (attribId == AttributeId.ATTRIB_SPACE) {
                    spc = decoder.readSpace();
                }
                else if (attribId == AttributeId.ATTRIB_FIRST) {
                    first = decoder.readUnsignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_LAST) {
                    last = decoder.readUnsignedInteger();
                    seenLast = true;
                }
                else if (attribId == AttributeId.ATTRIB_NAME) {
                    Translate trans = decoder.getAddrSpaceManager().getDefaultCodeSpace().getTrans();
                    VarnodeData point = trans.getRegister(decoder.readString());
                    spc = point.space;
                    first = point.offset;
                    last = (first - 1) + point.size;
                    // There should be no (space,first,last) attributes
                    return;
                }
            }
            if (spc == null) {
                throw new LowlevelError("No address space indicated in range tag");
            }
            if (!seenLast) {
                last = spc.getHighest();
            }
            if (first > spc.getHighest() || last > spc.getHighest() || last < first) {
                throw new LowlevelError("Illegal range tag");
            }
        }

        public int CompareTo(Range? other)
        {
            if ((other ?? throw new ArgumentNullException()) == this) {
                return 0;
            }
            return (this < other) ? -1 : 1;
        }
    }
}
