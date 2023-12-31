﻿using Sla.CORE;
using System.Text;

namespace Sla.DECCORE
{
    /// \brief Storage for decoding and storing strings associated with an address
    ///
    /// Looks at data in the loadimage to determine if it represents a "string".
    /// Decodes the string for presentation in the output.
    /// Stores the decoded string until its needed for presentation.
    internal abstract class StringManager
    {
        /// \brief String data (a sequence of bytes) stored by StringManager
        protected class StringData
        {
            /// \b true if the the string is truncated
            public bool isTruncated;
            ///< UTF8 encoded string data
            public List<byte> byteData = new List<byte>();
        }

        /// Map from address to string data
        protected Dictionary<Address, StringData> stringMap;
        /// Maximum characters in a string before truncating
        protected int maximumChars;

        /// \param max is the maximum number of characters to allow before truncating string
        public StringManager(int max)
        {
            maximumChars = max;
        }

        ~StringManager()
        {
            clear();
        }

        /// Clear out any cached strings
        public void clear()
        {
            stringMap.Clear();
        }

        // Determine if data at the given address is a string
        /// Returns \b true if the data is some kind of complete string.
        /// A given character data-type can be used as a hint for the encoding.
        /// The string decoding can be cached internally.
        /// \param addr is the given address
        /// \param charType is the given character data-type
        /// \return \b true if the address represents string data
        public bool isString(Address addr,Datatype charType)
        {
            // unused here
            bool isTrunc;
            List<byte> buffer = getStringData(addr, charType, out isTrunc);
            return (0 != buffer.Count);
        }

        /// \brief Retrieve string data at the given address as a UTF8 byte array
        ///
        /// If the address does not represent string data, a zero length List is returned. Otherwise,
        /// the string data is fetched, converted to a UTF8 encoding, cached and returned.
        /// \param addr is the given address
        /// \param charType is a character data-type indicating the encoding
        /// \param isTrunc passes back whether the string is truncated
        /// \return the byte array of UTF8 data
        public abstract List<byte> getStringData(Address addr, Datatype charType,
            out bool isTrunc);

        /// Encode cached strings to a stream
        /// Encode \<stringmanage> element, with \<string> children.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_STRINGMANAGE);

            foreach (KeyValuePair<Address, StringData> pair in stringMap) {
                encoder.openElement(ElementId.ELEM_STRING);
                pair.Key.encode(encoder);
                StringData stringData = pair.Value;
                encoder.openElement(ElementId.ELEM_BYTES);
                encoder.writeBool(AttributeId.ATTRIB_TRUNC, stringData.isTruncated);
                System.Text.StringBuilder s = new System.Text.StringBuilder();
                s.AppendLine();
                for (int i = 0; i < stringData.byteData.Count; ++i) {
                    s.AppendFormat("{0:X2}", (int)stringData.byteData[i]);
                    if (i % 20 == 19) {
                        s.AppendLine();
                        s.Append("  ");
                    }
                }
                s.AppendLine();
                encoder.writeString(AttributeId.ATTRIB_CONTENT, s.ToString());
                encoder.closeElement(ElementId.ELEM_BYTES);
            }
            encoder.closeElement(ElementId.ELEM_STRINGMANAGE);
        }

        /// Restore string cache from a stream
        /// Parse a \<stringmanage> element, with \<string> children.
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_STRINGMANAGE);
            while(true)
            {
                uint subId = decoder.openElement();
                if (subId != ElementId.ELEM_STRING) break;
                Address addr = Address.decode(decoder);
                StringData stringData = stringMap[addr];
                uint subId2 = decoder.openElement(ElementId.ELEM_BYTES);
                stringData.isTruncated = decoder.readBool(AttributeId.ATTRIB_TRUNC);
                TextReader @is = new StringReader(
                    decoder.readString(AttributeId.ATTRIB_CONTENT));
                int val;
                @is.ReadSpaces();
                int c1 = @is.Read();
                int c2 = @is.Read();
                while ((c1 > 0) && (c2 > 0)) {
                    if (c1 <= '9')
                        c1 = c1 - '0';
                    else if (c1 <= 'F')
                        c1 = c1 + 10 - 'A';
                    else
                        c1 = c1 + 10 - 'a';
                    if (c2 <= '9')
                        c2 = c2 - '0';
                    else if (c2 <= 'F')
                        c2 = c2 + 10 - 'A';
                    else
                        c2 = c2 + 10 - 'a';
                    val = c1 * 16 + c2;
                    stringData.byteData.Add((byte)val);
                    @is.ReadSpaces();
                    c1 = @is.Read();
                    c2 = @is.Read();
                }
                decoder.closeElement(subId2);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        /// Check for a unicode string terminator
        /// \param buffer is the byte buffer
        /// \param size is the number of bytes in the buffer
        /// \param charsize is the presumed size (in bytes) of character elements
        /// \return \b true if a string terminator is found
        public static bool hasCharTerminator(byte[] buffer, int offset, int size,int charsize)
        {
            for (int i = 0; i < size; i += charsize) {
                bool isTerminator = true;
                for (int j = 0; j < charsize; ++j) {
                    if (buffer[offset + i + j] != 0) {
                        // Non-zero bytes means character can't be a null terminator
                        isTerminator = false;
                        break;
                    }
                }
                if (isTerminator) return true;
            }
            return false;
        }

        /// Read a UTF16 code point from a byte array
        /// Pull the first two bytes from the byte array and combine them in the indicated endian order
        /// \param buf is the byte array
        /// \ADDED index
        /// \param bigend is \b true to request big endian encoding
        /// \return the decoded UTF16 element
        public static int readUtf16(byte[] buf, int index, bool bigend)
        {
            int codepoint;
            if (bigend) {
                codepoint = buf[index];
                codepoint <<= 8;
                codepoint += buf[index + 1];
            }
            else {
                codepoint = buf[index + 1];
                codepoint <<= 8;
                codepoint += buf[index];
            }
            return codepoint;
        }

        /// Write unicode character to stream in UTF8 encoding
        /// Encode the given unicode codepoint as UTF8 (1, 2, 3, or 4 bytes) and
        /// write the bytes to the stream.
        /// \param s is the output stream
        /// \param codepoint is the unicode codepoint
        public static void writeUtf8(TextWriter s, int codepoint)
        {
            byte[] bytes = new byte[4];
            int size;

            if (codepoint < 0)
                throw new LowlevelError("Negative unicode codepoint");
            if (codepoint < 128) {
                s.Write((byte)codepoint);
                return;
            }
            int bits = Globals.mostsigbit_set((ulong)codepoint) + 1;
            if (bits > 21)
                throw new LowlevelError("Bad unicode codepoint");
            if (bits < 12) {
                // Encode with two bytes
                bytes[0] = (byte)(0xc0 ^ ((codepoint >> 6) & 0x1f));
                bytes[1] = (byte)(0x80 ^ (codepoint & 0x3f));
                size = 2;
            }
            else if (bits < 17) {
                bytes[0] = (byte)(0xe0 ^ ((codepoint >> 12) & 0xf));
                bytes[1] = (byte)(0x80 ^ ((codepoint >> 6) & 0x3f));
                bytes[2] = (byte)(0x80 ^ (codepoint & 0x3f));
                size = 3;
            }
            else {
                bytes[0] = (byte)(0xf0 ^ ((codepoint >> 18) & 7));
                bytes[1] = (byte)(0x80 ^ ((codepoint >> 12) & 0x3f));
                bytes[2] = (byte)(0x80 ^ ((codepoint >> 6) & 0x3f));
                bytes[3] = (byte)(0x80 ^ (codepoint & 0x3f));
                size = 4;
            }
            // TODO : Check UTF8 encoding is appropriate.
            s.Write(Encoding.UTF8.GetString(bytes, 0, size));
        }

        /// Extract next \e unicode \e codepoint
        /// One or more bytes is consumed from the array, and the number of bytes used is passed back.
        /// \param buf is a pointer to the bytes in the character array
        /// \ADDED param index
        /// \param charsize is 1 for UTF8, 2 for UTF16, or 4 for UTF32
        /// \param bigend is \b true for big endian encoding of the UTF element
        /// \param skip is a reference for passing back the number of bytes consumed
        /// \return the codepoint or -1 if the encoding is invalid
        public static int getCodepoint(byte[] buf, int index, int charsize,bool bigend, int skip)
        {
            int codepoint;
            int sk = 0;
            if (charsize == 2) {
                // UTF-16
                codepoint = readUtf16(buf, 0, bigend);
                sk += 2;
                if ((codepoint >= 0xD800) && (codepoint <= 0xDBFF)) {
                    // high surrogate
                    int trail = readUtf16(buf, 2, bigend);
                    sk += 2;
                    if ((trail < 0xDC00) || (trail > 0xDFFF)) return -1; // Bad trail
                    codepoint = (codepoint << 10) + trail + (0x10000 - (0xD800 << 10) - 0xDC00);
                }
                else if ((codepoint >= 0xDC00) && (codepoint <= 0xDFFF)) return -1; // trail before high
            }
            else if (charsize == 1) {
                // UTF-8
                int val = buf[0];
                if ((val & 0x80) == 0) {
                    codepoint = val;
                    sk = 1;
                }
                else if ((val & 0xe0) == 0xc0) {
                    int val2 = buf[1];
                    sk = 2;
                    if ((val2 & 0xc0) != 0x80)
                        // Not a valid UTF8-encoding
                        return -1;
                    codepoint = ((val & 0x1f) << 6) | (val2 & 0x3f);
                }
                else if ((val & 0xf0) == 0xe0) {
                    int val2 = buf[1];
                    int val3 = buf[2];
                    sk = 3;
                    if (((val2 & 0xc0) != 0x80) || ((val3 & 0xc0) != 0x80))
                        // invalid encoding
                        return -1;
                    codepoint = ((val & 0xf) << 12) | ((val2 & 0x3f) << 6) | (val3 & 0x3f);
                }
                else if ((val & 0xf8) == 0xf0) {
                    int val2 = buf[1];
                    int val3 = buf[2];
                    int val4 = buf[3];
                    sk = 4;
                    if (((val2 & 0xc0) != 0x80) || ((val3 & 0xc0) != 0x80) || ((val4 & 0xc0) != 0x80)) return -1;   // invalid encoding
                    codepoint = ((val & 7) << 18) | ((val2 & 0x3f) << 12) | ((val3 & 0x3f) << 6) | (val4 & 0x3f);
                }
                else
                    return -1;
            }
            else if (charsize == 4) {
                // UTF-32
                sk = 4;
                if (bigend)
                    codepoint = (buf[0] << 24) + (buf[1] << 16) + (buf[2] << 8) + buf[3];
                else
                    codepoint = (buf[3] << 24) + (buf[2] << 16) + (buf[1] << 8) + buf[0];
            }
            else
                return -1;
            if (codepoint >= 0xd800 && codepoint <= 0xdfff)
                // Reserved for surrogates, invalid codepoints
                return -1;
            skip = sk;
            return codepoint;
        }
    }
}
