using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Protocol format for PackedEncode and PackedDecode classes
    /// All bytes in the encoding are expected to be non-zero.  Element encoding looks like
    ///   - 01xiiiii is an element start
    ///   - 10xiiiii is an element end
    ///   - 11xiiiii is an attribute start
    ///
    /// Where iiiii is the (first) 5 bits of the element/attribute id.
    /// If x=0, the id is complete.  If x=1, the next byte contains 7 more bits of the id:  1iiiiiii
    ///
    /// After an attribute start, there follows a \e type byte:  ttttllll, where the first 4 bits indicate the
    /// type of attribute and final 4 bits are a \b length \b code.  The types are:
    ///   - 1 = boolean (lengthcode=0 for false, lengthcode=1 for true)
    ///   - 2 = positive signed integer
    ///   - 3 = negative signed integer (stored in negated form)
    ///   - 4 = unsigned integer
    ///   - 5 = basic address space (encoded as the integer index of the space)
    ///   - 6 = special address space (lengthcode 0=>stack 1=>join 2=>fspec 3=>iop)
    ///   - 7 = string
    ///
    /// All attribute types except \e boolean and \e special, have an encoded integer after the \e type byte.
    /// The \b length \b code, indicates the number bytes used to encode the integer, 7-bits of info per byte, 1iiiiiii.
    /// A \b length \b code of zero is used to encode an integer value of 0, with no following bytes.
    ///
    /// For strings, the integer encoded after the \e type byte, is the actual length of the string.  The
    /// string data itself is stored immediately after the length integer using UTF8 format.
    internal static class PackedFormat
    {
        internal const byte HEADER_MASK = 0xc0;      ///< Bits encoding the record type
        internal const byte ELEMENT_START = 0x40;        ///< Header for an element start record
        internal const byte ELEMENT_END = 0x80;      ///< Header for an element end record
        internal const byte ATTRIBUTE = 0xc0;            ///< Header for an attribute record
        internal const byte HEADEREXTEND_MASK = 0x20;        ///< Bit indicating the id extends into the next byte
        internal const byte ELEMENTID_MASK = 0x1f;       ///< Bits encoding (part of) the id in the record header
        internal const byte RAWDATA_MASK = 0x7f;     ///< Bits of raw data in follow-on bytes
        internal const int RAWDATA_BITSPERBYTE = 7;      ///< Number of bits used in a follow-on byte
        internal const byte RAWDATA_MARKER = 0x80;       ///< The unused bit in follow-on bytes. (Always set to 1)
        internal const int TYPECODE_SHIFT = 4;           ///< Bit position of the type code in the type byte
        internal const byte LENGTHCODE_MASK = 0xf;       ///< Bits in the type byte forming the length code
        internal const byte TYPECODE_BOOLEAN = 1;        ///< Type code for the \e boolean type
        internal const byte TYPECODE_SIGNEDINT_POSITIVE = 2; ///< Type code for the \e signed \e positive \e integer type
        internal const byte TYPECODE_SIGNEDINT_NEGATIVE = 3; ///< Type code for the \e signed \e negative \e integer type
        internal const byte TYPECODE_UNSIGNEDINT = 4;        ///< Type code for the \e unsigned \e integer type
        internal const byte TYPECODE_ADDRESSSPACE = 5;       ///< Type code for the \e address \e space type
        internal const byte TYPECODE_SPECIALSPACE = 6;       ///< Type code for the \e special \e address \e space type
        internal const byte TYPECODE_STRING = 7;     ///< Type code for the \e string type
        internal const uint SPECIALSPACE_STACK = 0;      ///< Special code for the \e stack space
        internal const uint SPECIALSPACE_JOIN = 1;       ///< Special code for the \e join space
        internal const uint SPECIALSPACE_FSPEC = 2;      ///< Special code for the \e fspec space
        internal const uint SPECIALSPACE_IOP = 3;        ///< Special code for the \e iop space
        internal const uint SPECIALSPACE_SPACEBASE = 4;  ///< Special code for a \e spacebase space
    }
}
