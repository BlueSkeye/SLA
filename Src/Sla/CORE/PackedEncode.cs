using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A byte-based encoder designed to marshal from the decompiler efficiently
    /// See PackedDecode for details of the encoding format.
    public class PackedEncode : Encoder
    {
        ///< The stream receiving the encoded data
        private StreamWriter outStream;
        
        ///< Write a header, element or attribute, to stream
        /// \param header is the type of header
        /// \param id is the id associated with the element or attribute
        private void writeHeader(byte header, uint id)
        {
            if (id > 0x1f) {
                header |= PackedFormat.HEADEREXTEND_MASK;
                header |= (byte)(id >> PackedFormat.RAWDATA_BITSPERBYTE);
                byte extendByte =
                    (byte)((id & PackedFormat.RAWDATA_MASK) | PackedFormat.RAWDATA_MARKER);
                outStream.Write((char)header);
                outStream.Write((char)extendByte);
            }
            else {
                header |= (byte)id;
                outStream.Write((char)header);
            }
        }

        ///< Write an integer value to the stream
        /// The value is either an unsigned integer, an address space index, or (the absolute value of) a signed integer.
        /// A type header is passed in with the particular type code for the value already filled @in.
        /// This method then fills in the length code, outputs the full type header and the encoded bytes of the integer.
        /// \param typeByte is the type header
        /// \param val is the integer value
        private void writeInteger(byte typeByte, ulong val)
        {
            byte lenCode;
            int sa;
            if (val == 0) {
                lenCode = 0;
                sa = -1;
            }
            if (val < 0x800000000) {
                if (val < 0x200000) {
                    if (val < 0x80) {
                        lenCode = 1;        // 7-bits
                        sa = 0;
                    }
                    else if (val < 0x4000) {
                        lenCode = 2;        // 14-bits
                        sa = PackedFormat.RAWDATA_BITSPERBYTE;
                    }
                    else {
                        lenCode = 3;        // 21-bits
                        sa = 2 * PackedFormat.RAWDATA_BITSPERBYTE;
                    }
                }
                else if (val < 0x10000000) {
                    lenCode = 4;        // 28-bits
                    sa = 3 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
                else {
                    lenCode = 5;        // 35-bits
                    sa = 4 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
            }
            else if (val < 0x2000000000000) {
                if (val < 0x40000000000) {
                    lenCode = 6;
                    sa = 5 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
                else {
                    lenCode = 7;
                    sa = 6 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
            }
            else {
                if (val < 0x100000000000000) {
                    lenCode = 8;
                    sa = 7 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
                else if (val < 0x8000000000000000) {
                    lenCode = 9;
                    sa = 8 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
                else {
                    lenCode = 10;
                    sa = 9 * PackedFormat.RAWDATA_BITSPERBYTE;
                }
            }
            typeByte |= lenCode;
            outStream.Write((char)typeByte);
            for (; sa >= 0; sa -= PackedFormat.RAWDATA_BITSPERBYTE) {
                byte piece = (byte)((val >> sa) & PackedFormat.RAWDATA_MASK);
                piece |= PackedFormat.RAWDATA_MARKER;
                outStream.Write((char)piece);
            }
        }

        ///< Construct from a stream
        public PackedEncode(StreamWriter s)
        {
            outStream = s;
        }

        public override void openElement(ElementId elemId)
        {
            writeHeader(PackedFormat.ELEMENT_START, elemId.getId());
        }

        public override void closeElement(ElementId elemId)
        {
            writeHeader(PackedFormat.ELEMENT_END, elemId.getId());
        }

        public override void writeBool(AttributeId attribId, bool val)
        {
            writeHeader(PackedFormat.ATTRIBUTE, attribId.getId());
            byte typeByte = val
                ? (byte)((PackedFormat.TYPECODE_BOOLEAN << PackedFormat.TYPECODE_SHIFT) | 1)
                : (byte)(PackedFormat.TYPECODE_BOOLEAN << PackedFormat.TYPECODE_SHIFT);
            outStream.Write((char)typeByte);
        }

        public override void writeSignedInteger(AttributeId attribId, long val)
        {
            writeHeader(PackedFormat.ATTRIBUTE, attribId.getId());
            byte typeByte;
            ulong num;
            if (val < 0) {
                typeByte = (PackedFormat.TYPECODE_SIGNEDINT_NEGATIVE << PackedFormat.TYPECODE_SHIFT);
                num = (ulong)(-val);
            }
            else {
                typeByte = (PackedFormat.TYPECODE_SIGNEDINT_POSITIVE << PackedFormat.TYPECODE_SHIFT);
                num = (ulong)val;
            }
            writeInteger(typeByte, num);
        }

        public override void writeUnsignedInteger(AttributeId attribId, ulong val)
        {
            writeHeader(PackedFormat.ATTRIBUTE, attribId.getId());
            writeInteger((PackedFormat.TYPECODE_UNSIGNEDINT << PackedFormat.TYPECODE_SHIFT), val);
        }

        public override void writeString(AttributeId attribId, string val)
        {
            ulong length = (ulong)val.Length;
            writeHeader(PackedFormat.ATTRIBUTE, attribId.getId());
            writeInteger((PackedFormat.TYPECODE_STRING << PackedFormat.TYPECODE_SHIFT), length);
            outStream.Write(val);
        }

        public override void writeStringIndexed(AttributeId attribId, uint index, string val)
        {
            ulong length = (ulong)val.Length;
            writeHeader(PackedFormat.ATTRIBUTE, attribId.getId() + index);
            writeInteger((PackedFormat.TYPECODE_STRING << PackedFormat.TYPECODE_SHIFT), length);
            outStream.Write(val);
        }

        public override void writeSpace(AttributeId attribId, AddrSpace spc)
        {
            writeHeader(PackedFormat.ATTRIBUTE, attribId.getId());
            switch (spc.getType()) {
                case spacetype.IPTR_FSPEC:
                    outStream.Write(
                        (char)((PackedFormat.TYPECODE_SPECIALSPACE << PackedFormat.TYPECODE_SHIFT) | PackedFormat.SPECIALSPACE_FSPEC));
                    break;
                case spacetype.IPTR_IOP:
                    outStream.Write(
                        (char)((PackedFormat.TYPECODE_SPECIALSPACE << PackedFormat.TYPECODE_SHIFT) | PackedFormat.SPECIALSPACE_IOP));
                    break;
                case spacetype.IPTR_JOIN:
                    outStream.Write(
                        (char)((PackedFormat.TYPECODE_SPECIALSPACE << PackedFormat.TYPECODE_SHIFT) | PackedFormat.SPECIALSPACE_JOIN));
                    break;
                case spacetype.IPTR_SPACEBASE:
                    if (spc.isFormalStackSpace()) {
                        outStream.Write(
                            (char)((PackedFormat.TYPECODE_SPECIALSPACE << PackedFormat.TYPECODE_SHIFT) | PackedFormat.SPECIALSPACE_STACK));
                    }
                    else {
                        // A secondary register offset space
                        outStream.Write(
                            (char)((PackedFormat.TYPECODE_SPECIALSPACE << PackedFormat.TYPECODE_SHIFT) | PackedFormat.SPECIALSPACE_SPACEBASE));
                    }
                    break;
                default:
                    ulong spcId = (ulong)spc.getIndex();
                    writeInteger((PackedFormat.TYPECODE_ADDRESSSPACE << PackedFormat.TYPECODE_SHIFT),
                        spcId);
                    break;
            }
        }
    }
}
