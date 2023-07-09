using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief A byte-based decoder designed to marshal info to the decompiler efficiently
    /// The decoder expects an encoding as described in PackedFormat.  When ingested, the stream bytes are
    /// held in a sequence of arrays (ByteChunk). During decoding, \b this object maintains a Position in the
    /// stream at the start and end of the current open element, and a Position of the next attribute to read to
    /// facilitate getNextAttributeId() and associated read*() methods.
    public class PackedDecode : Decoder
    {
        ///< The size, in bytes, of a single cached chunk of the input stream
        public const int BUFFER_SIZE = 1024;

        /// \brief A bounded array of bytes <summary>
        /// \brief A bounded array of bytes
        /// </summary>
        private unsafe class ByteChunk
        {
            // friend class PackedDecode;
            /// Start of the byte array
            internal byte* start;
            /// End of the byte array
            internal byte* end;

            ///< Constructor
            public ByteChunk(byte* s, byte* e)
            {
                start = s;
                end = e;
            }
        }

        /// \brief An iterator into input stream
        private unsafe class Position
        {
            // friend class PackedDecode;
            /// Current byte sequence
            internal IEnumerator<ByteChunk> seqIter;
            /// Current position in sequence
            internal byte* current;
            /// End of current sequence
            internal byte* end;
        }

        /// Incoming raw data as a sequence of byte arrays
        private List<ByteChunk> inStream;
        /// Position at the start of the current open element
        private Position startPos;
        /// Position of the next attribute as returned by getNextAttributeId
        private Position curPos;
        /// Ending position after all attributes in current open element
        private Position endPos;
        /// Has the last attribute returned by getNextAttributeId been read
        private bool attributeRead;

        /// Get the byte at the current position, do not advance
        private unsafe byte getByte(Position pos)
        {
            return *pos.current;
        }

        ///< Get the byte following the current byte, do not advance position
        /// An exception is thrown if the position currently points to the last byte in the stream
        /// \param pos is the position in the stream to look ahead from
        /// \return the next byte
        private unsafe byte getBytePlus1(Position pos)
        {
            byte* ptr = pos.current + 1;
            if (ptr == pos.end) {
                IEnumerator<ByteChunk> iter = pos.seqIter;
                if (!iter.MoveNext()) {
                    throw new DecoderError("Unexpected end of stream");
                }
                ptr = iter.Current.start;
            }
            return *ptr;
        }

        ///< Get the byte at the current position and advance to the next byte
        /// An exception is thrown if there are no additional bytes in the stream
        /// \param pos is the position of the byte
        /// \return the byte at the current position
        private unsafe byte getNextByte(Position pos)
        {
            byte res = *pos.current;
            pos.current += 1;
            if (pos.current != pos.end) {
                return res;
            }
            if (!pos.seqIter.MoveNext()) {
                throw new DecoderError("Unexpected end of stream");
            }
            pos.current = pos.seqIter.Current.start;
            pos.end = pos.seqIter.Current.end;
            return res;
        }

        /// Advance the position by the given number of bytes
        /// An exception is thrown of position is advanced past the end of the stream
        /// \param pos is the position being advanced
        /// \param skip is the number of bytes to advance
        private unsafe void advancePosition(Position pos, int skip)
        {
            while (pos.end - pos.current <= skip) {
                skip -= (int)(pos.end - pos.current);
                if (!pos.seqIter.MoveNext()) {
                    throw new DecoderError("Unexpected end of stream");
                }
                pos.current = pos.seqIter.Current.start;
                pos.end = pos.seqIter.Current.end;
            }
            pos.current += skip;
        }

        /// Read an integer from the \e current position given its length in bytes
        /// The integer is encoded, 7-bits per byte, starting with the most significant 7-bits.
        /// The integer is decode from the \e current position, and the position is advanced.
        /// \param len is the number of bytes to extract
        private ulong readInteger(uint len)
        {
            ulong res = 0;
            while (len > 0) {
                res <<= PackedFormat.RAWDATA_BITSPERBYTE;
                res |= (byte)(getNextByte(curPos) & PackedFormat.RAWDATA_MASK);
                len -= 1;
            }
            return res;
        }

        /// Extract length code from type byte
        private uint readLengthCode(byte typeByte)
        {
            return ((uint)typeByte & PackedFormat.LENGTHCODE_MASK);
        }

        /// Find attribute matching the given id in open element
        /// The \e current position is reset to the start of the current open element. Attributes are scanned
        /// and skipped until the attribute matching the given id is found.  The \e current position is set to the
        /// start of the matching attribute, in preparation for one of the read*() methods.
        /// If the id is not found an exception is thrown.
        /// \param attribId is the attribute id to scan for.
        private void findMatchingAttribute(AttributeId attribId)
        {
            curPos = startPos;
            while(true) {
                byte header1 = getByte(curPos);
                if ((header1 & PackedFormat.HEADER_MASK) != PackedFormat.ATTRIBUTE) {
                    throw new DecoderError($"Attribute {attribId.getName()} is not present");
                }
                uint id = (byte)(header1 & PackedFormat.ELEMENTID_MASK);
                if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                    id <<= PackedFormat.RAWDATA_BITSPERBYTE;
                    id |= (byte)(getBytePlus1(curPos) & PackedFormat.RAWDATA_MASK);
                }
                if (attribId.getId() == id) {
                    // Found it
                    return;
                }
                skipAttribute();
            }
        }

        ///< Skip over the attribute at the current position
        /// The attribute at the \e current position is scanned enough to determine its length, and the position
        /// is advanced to the following byte.
        private void skipAttribute()
        {
            // Attribute header
            byte header1 = getNextByte(curPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                // Extra byte for extended id
                getNextByte(curPos);
            }
            // Type (and length) byte
            byte typeByte = getNextByte(curPos);
            byte attribType = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            if ((attribType == PackedFormat.TYPECODE_BOOLEAN)
                || (attribType == PackedFormat.TYPECODE_SPECIALSPACE))
            {
                // has no additional data
                return;
            }
            // Length of data in bytes
            uint length = readLengthCode(typeByte);
            if (attribType == PackedFormat.TYPECODE_STRING) {
                // Read length field to get final length of string
                length = (uint)readInteger(length);
            }
            // Skip -length- data
            advancePosition(curPos, (int)length);
        }

        ///< Skip over remaining attribute data, after a mismatch
        /// This assumes the header and \b type \b byte have been read.  Decode type and length info and finish
        /// skipping over the attribute so that the next call to getNextAttributeId() is on cut.
        /// \param typeByte is the previously scanned type byte
        private void skipAttributeRemaining(byte typeByte)
        {
            byte attribType = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            if ((attribType == PackedFormat.TYPECODE_BOOLEAN)
                || (attribType == PackedFormat.TYPECODE_SPECIALSPACE))
            {
                // has no additional data
                return;
            }
            // Length of data in bytes
            uint length = readLengthCode(typeByte);
            if (attribType == PackedFormat.TYPECODE_STRING) {
                // Read length field to get final length of string
                length = (uint)readInteger(length);
            }
            // Skip -length- data
            advancePosition(curPos, (int)length);
        }

        ///< Constructor
        public PackedDecode(AddrSpaceManager spcManager)
            : base(spcManager)
        {
        }

        ~PackedDecode()
        {
            foreach (ByteChunk chunk in inStream) {
                throw new NotImplementedException();
                // delete[]chunk.start;
            }
        }

        public unsafe override void ingestStream(StreamReader s)
        {
            int gcount = 0;
            while (0 < s.Peek()) {
                byte[] buf = new byte[BUFFER_SIZE + 1];
                fixed(byte* pBuffer = buf) {
                    inStream.Add(new ByteChunk(pBuffer, pBuffer + BUFFER_SIZE));
                }
                s.get(buf, BUFFER_SIZE + 1, out gcount, '\0');
            }
            endPos.seqIter = inStream.GetEnumerator();
            if (endPos.seqIter.MoveNext()) {
                endPos.current = endPos.seqIter.Current.start;
                endPos.end = endPos.seqIter.Current.end;
                // Make sure there is at least one character after ingested buffer
                if (gcount == BUFFER_SIZE) {
                    // Last buffer was entirely filled
                    // Add one more buffer
                    byte[] endbuf = new byte[1];
                    fixed (byte* pBuffer = endbuf) {
                        inStream.Add(new ByteChunk(pBuffer, pBuffer + 1));
                    }
                    gcount = 0;
                }
                byte* buf = inStream[inStream.Count - 1].start;
                buf[gcount] = PackedFormat.ELEMENT_END;
            }
        }

        public override uint peekElement()
        {
            byte header1 = getByte(endPos);
            if ((header1 & PackedFormat.HEADER_MASK) != PackedFormat.ELEMENT_START) {
                return 0;
            }
            uint id = (byte)(header1 & PackedFormat.ELEMENTID_MASK);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                id <<= PackedFormat.RAWDATA_BITSPERBYTE;
                id |= (uint)(getBytePlus1(endPos) & PackedFormat.RAWDATA_MASK);
            }
            return id;
        }

        public override uint openElement()
        {
            byte header1 = getByte(endPos);
            if ((header1 & PackedFormat.HEADER_MASK) != PackedFormat.ELEMENT_START) {
                return 0;
            }
            getNextByte(endPos);
            uint id = (byte)(header1 & PackedFormat.ELEMENTID_MASK);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                id <<= PackedFormat.RAWDATA_BITSPERBYTE;
                id |= (byte)(getNextByte(endPos) & PackedFormat.RAWDATA_MASK);
            }
            startPos = endPos;
            curPos = endPos;
            header1 = getByte(curPos);
            while ((header1 & PackedFormat.HEADER_MASK) == PackedFormat.ATTRIBUTE) {
                skipAttribute();
                header1 = getByte(curPos);
            }
            endPos = curPos;
            curPos = startPos;
            // "Last attribute was read" is vacuously true
            attributeRead = true;
            return id;
        }

        public override uint openElement(ElementId elemId)
        {
            uint id = openElement();
            if (id != elemId.getId())
            {
                if (id == 0)
                {
                    throw new DecoderError(
                        "Expecting <" + elemId.getName() + "> but did not scan an element");
                }
                throw new DecoderError("Expecting <" + elemId.getName() + "> but id did not match");
            }
            return id;
        }

        public override void closeElement(uint id)
        {
            byte header1 = getNextByte(endPos);
            if ((header1 & PackedFormat.HEADER_MASK) != PackedFormat.ELEMENT_END)
            {
                throw new DecoderError("Expecting element close");
            }
            uint closeId = (byte)(header1 & PackedFormat.ELEMENTID_MASK);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0)
            {
                closeId <<= PackedFormat.RAWDATA_BITSPERBYTE;
                closeId |= (byte)(getNextByte(endPos) & PackedFormat.RAWDATA_MASK);
            }
            if (id != closeId)
            {
                throw new DecoderError("Did not see expected closing element");
            }
        }

        public override void closeElementSkipping(uint id)
        {
            List<uint> idstack = new List<uint>();
            idstack.Add(id);
            do
            {
                byte header1 = (byte)(getByte(endPos) & PackedFormat.HEADER_MASK);
                if (header1 == PackedFormat.ELEMENT_END)
                {
                    int lastIndex = idstack.Count - 1;
                    closeElement(idstack[lastIndex]);
                    idstack.RemoveAt(lastIndex);
                }
                else if (header1 == PackedFormat.ELEMENT_START)
                {
                    idstack.Add(openElement());
                }
                else
                {
                    throw new DecoderError("Corrupt stream");
                }
            } while (0 != idstack.Count);
        }

        public override void rewindAttributes()
        {
            curPos = startPos;
            attributeRead = true;
        }

        public override uint getNextAttributeId()
        {
            if (!attributeRead)
            {
                skipAttribute();
            }
            byte header1 = getByte(curPos);
            if ((header1 & PackedFormat.HEADER_MASK) != PackedFormat.ATTRIBUTE)
            {
                return 0;
            }
            uint id = (byte)(header1 & PackedFormat.ELEMENTID_MASK);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0)
            {
                id <<= PackedFormat.RAWDATA_BITSPERBYTE;
                id |= (uint)(getBytePlus1(curPos) & PackedFormat.RAWDATA_MASK);
            }
            attributeRead = false;
            return id;
        }

        public override uint getIndexedAttributeId(AttributeId attribId)
        {
            // PackedDecode never needs to reinterpret an attribute
            return AttributeId.ATTRIB_UNKNOWN.getId();
        }

        public override bool readBool()
        {
            byte header1 = getNextByte(curPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0)
            {
                getNextByte(curPos);
            }
            byte typeByte = getNextByte(curPos);
            attributeRead = true;
            if ((typeByte >> PackedFormat.TYPECODE_SHIFT) != PackedFormat.TYPECODE_BOOLEAN)
            {
                throw new DecoderError("Expecting boolean attribute");
            }
            return ((typeByte & PackedFormat.LENGTHCODE_MASK) != 0);
        }

        public override bool readBool(ref AttributeId attribId)
        {
            findMatchingAttribute(attribId);
            bool res = readBool();
            curPos = startPos;
            return res;
        }

        public override long readSignedInteger()
        {
            byte header1 = getNextByte(curPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0)
            {
                getNextByte(curPos);
            }
            byte typeByte = getNextByte(curPos);
            uint typeCode = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            long res;
            if (typeCode == PackedFormat.TYPECODE_SIGNEDINT_POSITIVE)
            {
                res = (long)readInteger(readLengthCode(typeByte));
            }
            else if (typeCode == PackedFormat.TYPECODE_SIGNEDINT_NEGATIVE)
            {
                res = (long)readInteger(readLengthCode(typeByte));
                res = -res;
            }
            else
            {
                skipAttributeRemaining(typeByte);
                attributeRead = true;
                throw new DecoderError("Expecting signed integer attribute");
            }
            attributeRead = true;
            return res;
        }

        public override long readSignedInteger(AttributeId attribId)
        {
            findMatchingAttribute(attribId);
            long res = readSignedInteger();
            curPos = startPos;
            return res;
        }

        public override long readSignedIntegerExpectString(string expect, long expectval)
        {
            Position tmpPos = curPos;
            byte header1 = getNextByte(tmpPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                getNextByte(tmpPos);
            }
            byte typeByte = getNextByte(tmpPos);
            uint typeCode = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            if (typeCode == PackedFormat.TYPECODE_STRING) {
                string val = readString();
                if (val != expect) {
                    throw new DecoderError($"Expecting string \"{expect}\" but read \"{val}\"");
                }
                return expectval;
            }
            return readSignedInteger();
        }

        public override long readSignedIntegerExpectString(ref AttributeId attribId,
            ref string expect, long expectval)
        {
            findMatchingAttribute(attribId);
            long res = readSignedIntegerExpectString(expect, expectval);
            curPos = startPos;
            return res;
        }

        public override ulong readUnsignedInteger()
        {
            byte header1 = getNextByte(curPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0)
            {
                getNextByte(curPos);
            }
            byte typeByte = getNextByte(curPos);
            uint typeCode = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            if (typeCode != PackedFormat.TYPECODE_UNSIGNEDINT)
            {
                skipAttributeRemaining(typeByte);
                attributeRead = true;
                throw new DecoderError("Expecting unsigned integer attribute");
            }
            ulong res = readInteger(readLengthCode(typeByte));
            attributeRead = true;
            return res;
        }

        public override ulong readUnsignedInteger(AttributeId attribId)
        {
            findMatchingAttribute(attribId);
            ulong res = readUnsignedInteger();
            curPos = startPos;
            return res;
        }

        public unsafe override string readString()
        {
            byte header1 = getNextByte(curPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                getNextByte(curPos);
            }
            byte typeByte = getNextByte(curPos);
            uint typeCode = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            if (typeCode != PackedFormat.TYPECODE_STRING) {
                skipAttributeRemaining(typeByte);
                attributeRead = true;
                throw new DecoderError("Expecting string attribute");
            }
            int length = (int)readLengthCode(typeByte);
            length = (int)readInteger((uint)length);

            attributeRead = true;
            int curLen = (int)(curPos.end - curPos.current);
            string res;
            if (curLen >= length) {
                res = Marshal.PtrToStringUTF8((nint)(void*)curPos.current, length);
                advancePosition(curPos, length);
                return res;
            }
            res = new string((char*)curPos.current, 0, curLen);
            length -= curLen;
            advancePosition(curPos, curLen);
            while (length > 0) {
                curLen = (int)(curPos.end - curPos.current);
                if (curLen > length) {
                    curLen = length;
                }
                res += new string((char*)curPos.current, 0, curLen);
                length -= curLen;
                advancePosition(curPos, curLen);
            }
            return res;
        }

        public override string readString(AttributeId attribId)
        {
            findMatchingAttribute(attribId);
            string res = readString();
            curPos = startPos;
            return res;
        }

        public override AddrSpace readSpace()
        {
            byte header1 = getNextByte(curPos);
            if ((header1 & PackedFormat.HEADEREXTEND_MASK) != 0) {
                getNextByte(curPos);
            }
            byte typeByte = getNextByte(curPos);
            uint typeCode = (byte)(typeByte >> PackedFormat.TYPECODE_SHIFT);
            int res;
            AddrSpace spc;
            if (typeCode == PackedFormat.TYPECODE_ADDRESSSPACE) {
                res = (int)readInteger(readLengthCode(typeByte));
                spc = spcManager.getSpace(res);
                if (spc == null) {
                    throw new DecoderError("Unknown address space index");
                }
            }
            else if (typeCode == PackedFormat.TYPECODE_SPECIALSPACE) {
                uint specialCode = readLengthCode(typeByte);
                if (specialCode == PackedFormat.SPECIALSPACE_STACK)
                    spc = spcManager.getStackSpace();
                else if (specialCode == PackedFormat.SPECIALSPACE_JOIN) {
                    spc = spcManager.getJoinSpace();
                }
                else {
                    throw new DecoderError("Cannot marshal special address space");
                }
            }
            else
            {
                skipAttributeRemaining(typeByte);
                attributeRead = true;
                throw new DecoderError("Expecting space attribute");
            }
            attributeRead = true;
            return spc;
        }

        public unsafe override AddrSpace readSpace(AttributeId attribId)
        {
            findMatchingAttribute(attribId);
            AddrSpace res = readSpace();
            curPos = startPos;
            return res;
        }
    }
}
