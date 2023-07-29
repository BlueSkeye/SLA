using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

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
            public List<byte> byteData;
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
            stringMap.clear();
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
            bool isTrunc;       // unused here
            List<byte> buffer = getStringData(addr, charType, isTrunc);
            return !buffer.empty();
        }

        /// \brief Retrieve string data at the given address as a UTF8 byte array
        ///
        /// If the address does not represent string data, a zero length List is returned. Otherwise,
        /// the string data is fetched, converted to a UTF8 encoding, cached and returned.
        /// \param addr is the given address
        /// \param charType is a character data-type indicating the encoding
        /// \param isTrunc passes back whether the string is truncated
        /// \return the byte array of UTF8 data
        public abstract List<byte> getStringData(Address addr, Datatype charType, bool isTrunc);

        /// Encode cached strings to a stream
        /// Encode \<stringmanage> element, with \<string> children.
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_STRINGMANAGE);

            map<Address, StringData>::const_iterator iter1;
            for (iter1 = stringMap.begin(); iter1 != stringMap.end(); ++iter1)
            {
                encoder.openElement(ELEM_STRING);
                (*iter1).first.encode(encoder);
                StringData stringData = (*iter1).second;
                encoder.openElement(ELEM_BYTES);
                encoder.writeBool(ATTRIB_TRUNC, stringData.isTruncated);
                ostringstream s;
                s << '\n' << setfill('0');
                for (int i = 0; i < stringData.byteData.size(); ++i)
                {
                    s << hex << setw(2) << (int)stringData.byteData[i];
                    if (i % 20 == 19)
                        s << "\n  ";
                }
                s << '\n';
                encoder.writeString(ATTRIB_CONTENT, s.str());
                encoder.closeElement(ELEM_BYTES);
            }
            encoder.closeElement(ELEM_STRINGMANAGE);
        }

        /// Restore string cache from a stream
        /// Parse a \<stringmanage> element, with \<string> children.
        /// \param decoder is the stream decoder
        public void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_STRINGMANAGE);
            for (; ; )
            {
                uint subId = decoder.openElement();
                if (subId != ELEM_STRING) break;
                Address addr = Address::decode(decoder);
                StringData & stringData(stringMap[addr]);
                uint subId2 = decoder.openElement(ELEM_BYTES);
                stringData.isTruncated = decoder.readBool(ATTRIB_TRUNC);
                istringstream @is = new istringstream(decoder.readString(ATTRIB_CONTENT));
                int val;
                char c1, c2;
                @is >> ws;
                c1 = @is.get();
                c2 = @is.get();
                while ((c1 > 0) && (c2 > 0))
                {
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
                    stringData.byteData.push_back((byte)val);
                    @is >> ws;
                    c1 = @is.get();
                    c2 = @is.get();
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
        public static bool hasCharTerminator(byte buffer, int size,int charsize)
        {
            for (int i = 0; i < size; i += charsize)
            {
                bool isTerminator = true;
                for (int j = 0; j < charsize; ++j)
                {
                    if (buffer[i + j] != 0)
                    {   // Non-zero bytes means character can't be a null terminator
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
        /// \param bigend is \b true to request big endian encoding
        /// \return the decoded UTF16 element
        public static int readUtf16(byte buf,bool bigend)
        {
            int codepoint;
            if (bigend)
            {
                codepoint = buf[0];
                codepoint <<= 8;
                codepoint += buf[1];
            }
            else
            {
                codepoint = buf[1];
                codepoint <<= 8;
                codepoint += buf[0];
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
            byte bytes[4];
            int size;

            if (codepoint < 0)
                throw new LowlevelError("Negative unicode codepoint");
            if (codepoint < 128)
            {
                s.put((byte)codepoint);
                return;
            }
            int bits = mostsigbit_set(codepoint) + 1;
            if (bits > 21)
                throw new LowlevelError("Bad unicode codepoint");
            if (bits < 12)
            {   // Encode with two bytes
                bytes[0] = 0xc0 ^ ((codepoint >> 6) & 0x1f);
                bytes[1] = 0x80 ^ (codepoint & 0x3f);
                size = 2;
            }
            else if (bits < 17)
            {
                bytes[0] = 0xe0 ^ ((codepoint >> 12) & 0xf);
                bytes[1] = 0x80 ^ ((codepoint >> 6) & 0x3f);
                bytes[2] = 0x80 ^ (codepoint & 0x3f);
                size = 3;
            }
            else
            {
                bytes[0] = 0xf0 ^ ((codepoint >> 18) & 7);
                bytes[1] = 0x80 ^ ((codepoint >> 12) & 0x3f);
                bytes[2] = 0x80 ^ ((codepoint >> 6) & 0x3f);
                bytes[3] = 0x80 ^ (codepoint & 0x3f);
                size = 4;
            }
            s.write((char*)bytes, size);
        }

        /// Extract next \e unicode \e codepoint
        /// One or more bytes is consumed from the array, and the number of bytes used is passed back.
        /// \param buf is a pointer to the bytes in the character array
        /// \param charsize is 1 for UTF8, 2 for UTF16, or 4 for UTF32
        /// \param bigend is \b true for big endian encoding of the UTF element
        /// \param skip is a reference for passing back the number of bytes consumed
        /// \return the codepoint or -1 if the encoding is invalid
        public static int getCodepoint(byte buf, int charsize,bool bigend, int skip)
        {
            int codepoint;
            int sk = 0;
            if (charsize == 2)
            {       // UTF-16
                codepoint = readUtf16(buf, bigend);
                sk += 2;
                if ((codepoint >= 0xD800) && (codepoint <= 0xDBFF))
                { // high surrogate
                    int trail = readUtf16(buf + 2, bigend);
                    sk += 2;
                    if ((trail < 0xDC00) || (trail > 0xDFFF)) return -1; // Bad trail
                    codepoint = (codepoint << 10) + trail + (0x10000 - (0xD800 << 10) - 0xDC00);
                }
                else if ((codepoint >= 0xDC00) && (codepoint <= 0xDFFF)) return -1; // trail before high
            }
            else if (charsize == 1)
            {   // UTF-8
                int val = buf[0];
                if ((val & 0x80) == 0)
                {
                    codepoint = val;
                    sk = 1;
                }
                else if ((val & 0xe0) == 0xc0)
                {
                    int val2 = buf[1];
                    sk = 2;
                    if ((val2 & 0xc0) != 0x80) return -1; // Not a valid UTF8-encoding
                    codepoint = ((val & 0x1f) << 6) | (val2 & 0x3f);
                }
                else if ((val & 0xf0) == 0xe0)
                {
                    int val2 = buf[1];
                    int val3 = buf[2];
                    sk = 3;
                    if (((val2 & 0xc0) != 0x80) || ((val3 & 0xc0) != 0x80)) return -1; // invalid encoding
                    codepoint = ((val & 0xf) << 12) | ((val2 & 0x3f) << 6) | (val3 & 0x3f);
                }
                else if ((val & 0xf8) == 0xf0)
                {
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
            else if (charsize == 4)
            {   // UTF-32
                sk = 4;
                if (bigend)
                    codepoint = (buf[0] << 24) + (buf[1] << 16) + (buf[2] << 8) + buf[3];
                else
                    codepoint = (buf[3] << 24) + (buf[2] << 16) + (buf[1] << 8) + buf[0];
            }
            else
                return -1;
            if (codepoint >= 0xd800 && codepoint <= 0xdfff)
                return -1;      // Reserved for surrogates, invalid codepoints
            skip = sk;
            return codepoint;
        }
    }
}
