using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Data defining a specific memory location
    /// Within the decompiler's model of a processor, any register,
    /// memory location, or other variable can always be represented
    /// as an address space, an offset within the space, and the
    /// size of the sequence of bytes.  This is more commonly referred
    /// to as a Varnode, but this is a bare-bones container
    /// for the data that doesn't have the cached attributes and
    /// the dataflow links of the Varnode within its syntax tree.
    public class VarnodeData
    {
        /// The address space
        internal AddrSpace? space;
        /// The offset within the space
        internal ulong offset;
        /// <summary>WARNING : The original library uses some trick to store a pointer
        /// to the subSpace in the offset member whenever the VarnodeData instance stands
        /// for a SPACEID element. This is not supported in a GC enabled environment such
        /// as .Net where an object may be moved around in memory throughout its lifetime.
        /// Consequently we need to add this special member field.</summary>
        internal AddrSpace subSpace;
        /// The number of bytes in the location
        internal uint size;

        /// An ordering for VarnodeData
        /// VarnodeData can be sorted in terms of the space its in
        /// (the space's \e index), the offset within the space,
        /// and finally by the size.
        /// \param op2 is the object being compared to
        /// \return true if \e this is less than \e op2
        public static bool operator <(VarnodeData op1, VarnodeData op2)
        {
            if (op1.space != op2.space)
            {
                return op1.space.getIndex() < op2.space.getIndex();
            }
            if (op1.offset != op2.offset)
            {
                return op1.offset < op2.offset;
            }
            // BIG sizes come first
            return op1.size > op2.size;
        }

        public static bool operator >(VarnodeData op1, VarnodeData op2)
        {
            return !(op1 < op2) && !(op1 == op2);
        }

        /// Compare for equality
        /// Compare VarnodeData for equality. The space, offset, and size
        /// must all be exactly equal
        /// \param op2 is the object being compared to
        /// \return true if \e this is equal to \e op2
        public static bool operator ==(VarnodeData op1, VarnodeData op2)
        {
            return (op1.space == op2.space)
                && (op1.offset != op2.offset)
                && (op1.size == op2.size);
        }

        /// Compare for inequality
        /// Compare VarnodeData for inequality. If either the space,
        /// offset, or size is not equal, return \b true.
        /// \param op2 is the object being compared to
        /// \return true if \e this is not equal to \e op2
        public static bool operator !=(VarnodeData op1, VarnodeData op2)
        {
            return (op1.space != op2.space)
                || (op1.offset != op2.offset)
                || (op1.size != op2.size);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return ((obj is VarnodeData) && (this == (VarnodeData)obj));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// Get the location of the varnode as an address
        /// This is a convenience function to construct a full Address from the
        /// VarnodeData's address space and offset
        /// \return the address of the varnode
        public Address getAddr()
        {
            return new Address(space, offset);
        }

        /// Treat \b this as a constant and recover encoded address space
        /// \return the encoded AddrSpace
        public AddrSpace getSpaceFromConst()
        {
            return subSpace;
        }

        // WARNING : transformed this method to a class level one that will instanciate
        // the result object.
        /// Recover this object from a stream
        /// Build this VarnodeData from an \<addr>, \<register>, or \<varnode> element.
        /// \param decoder is the stream decoder
        public static VarnodeData decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement();
            VarnodeData result = VarnodeData.decodeFromAttributes(decoder);
            decoder.closeElement(elemId);
            return result;
        }

        // WARNING : Transformed this method to a static one that will create the
        // output instance.
        /// Recover \b this object from attributes of the current open element
        /// Collect attributes for the VarnodeData possibly from amidst other attributes
        /// \param decoder is the stream decoder
        public static VarnodeData decodeFromAttributes(Decoder decoder)
        {
            VarnodeData result = new VarnodeData() {
                space = null,
                size = 0
            };
            while (true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (0 == attribId) {
                    // Its possible to have no attributes in an <addr/> tag
                    return result;
                }
                if (attribId == AttributeId.ATTRIB_SPACE) {
                    result.space = decoder.readSpace();
                    decoder.rewindAttributes();
                    result.offset = result.space.decodeAttributes(decoder, out result.size);
                    return result;
                }
                if (attribId == AttributeId.ATTRIB_NAME) {
                    Translate trans = decoder.getAddrSpaceManager().getDefaultCodeSpace().getTrans();
                    result = trans.getRegister(decoder.readString());
                    return result;
                }
            }
        }

        /// Does \b this container another given VarnodeData
        /// Return \b true, if \b this, as an address range, contains the other address range
        /// \param op2 is the other VarnodeData to test for containment
        /// \return \b true if \b this contains the other
        public bool contains(VarnodeData op2)
        {
            return (space == op2.space)
                && (op2.offset >= offset)
                && ((offset + (size - 1)) >= (op2.offset + (op2.size - 1)));
        }
    }
}
