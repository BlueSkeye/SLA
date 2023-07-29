using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An implementation of StringManager that understands terminated unicode strings
    ///
    /// This class understands UTF8, UTF16, and UTF32 encodings.  It reports a string if its
    /// sees a valid encoding that is null terminated.
    internal class StringManagerUnicode : StringManager
    {
        /// Underlying architecture
        private Architecture glb;
        /// Temporary buffer for pulling in loadimage bytes
        private uint1[] testBuffer;

        /// Make sure buffer has valid bounded set of unicode
        /// Check that the given buffer contains valid unicode.
        /// If the string is encoded in UTF8 or ASCII, we get (on average) a bit of check
        /// per character.  For UTF16, the surrogate reserved area gives at least some check.
        /// \param buf is the byte array to check
        /// \param size is the size of the buffer in bytes
        /// \param charsize is the UTF encoding (1=UTF8, 2=UTF16, 4=UTF32)
        /// \return the number of characters or -1 if there is an invalid encoding
        private int4 checkCharacters(uint1[] buf, int4 size,int4 charsize)
        {
            if (buf == (uint1*)0) return -1;
            bool bigend = glb.translate.isBigEndian();
            int4 i = 0;
            int4 count = 0;
            int4 skip = charsize;
            while (i < size)
            {
                int4 codepoint = getCodepoint(buf + i, charsize, bigend, skip);
                if (codepoint < 0) return -1;
                if (codepoint == 0) break;
                count += 1;
                i += skip;
            }
            return count;
        }

        /// \param g is the underlying architecture (and loadimage)
        /// \param max is the maximum number of bytes to allow in a decoded string
        public StringManagerUnicode(Architecture g, int4 max)
            : base(max)
        {
            glb = g;
            testBuffer = new uint1[max];
        }

        ~StringManagerUnicode()
        {
            delete[] testBuffer;
        }

        public override List<uint1> getStringData(Address addr, Datatype charType, bool isTrunc)
        {
            map<Address, StringData>::iterator iter;
            iter = stringMap.find(addr);
            if (iter != stringMap.end())
            {
                isTrunc = (*iter).second.isTruncated;
                return (*iter).second.byteData;
            }

            StringData & stringData(stringMap[addr]);       // Allocate (initially empty) byte vector
            stringData.isTruncated = false;
            isTrunc = false;

            if (charType.isOpaqueString())     // Cannot currently test for an opaque encoding
                return stringData.byteData;         // Return the empty buffer

            int4 curBufferSize = 0;
            int4 charsize = charType.getSize();
            bool foundTerminator = false;

            try
            {
                do
                {
                    int4 amount = 32;   // Grab 32 bytes of image at a time
                    uint4 newBufferSize = curBufferSize + amount;
                    if (newBufferSize > maximumChars)
                    {
                        newBufferSize = maximumChars;
                        amount = newBufferSize - curBufferSize;
                        if (amount == 0)
                        {
                            return stringData.byteData;     // Could not find terminator
                        }
                    }
                    glb.loader.loadFill(testBuffer + curBufferSize, amount,
                              addr + curBufferSize);
                    foundTerminator = hasCharTerminator(testBuffer + curBufferSize, amount,
                                    charsize);
                    curBufferSize = newBufferSize;
                } while (!foundTerminator);
            }
            catch (DataUnavailError err) {
                return stringData.byteData;         // Return the empty buffer
            }

            int4 numChars = checkCharacters(testBuffer, curBufferSize, charsize);
            if (numChars < 0)
                return stringData.byteData;     // Return the empty buffer (invalid encoding)
            if (charsize == 1 && numChars < maximumChars)
            {
                stringData.byteData.reserve(curBufferSize);
                stringData.byteData.assign(testBuffer, testBuffer + curBufferSize);
            }
            else
            {
                // We need to translate to UTF8 and/or truncate
                ostringstream s;
                if (!writeUnicode(s, testBuffer, curBufferSize, charsize))
                    return stringData.byteData;     // Return the empty buffer
                string resString = s.str();
                int4 newSize = resString.size();
                stringData.byteData.reserve(newSize + 1);
                uint1* ptr = (uint1*)resString.c_str();
                stringData.byteData.assign(ptr, ptr + newSize);
                stringData.byteData[newSize] = 0;       // Make sure there is a null terminator
            }
            stringData.isTruncated = (numChars >= maximumChars);
            isTrunc = stringData.isTruncated;
            return stringData.byteData;
        }

        /// Translate/copy unicode to UTF8
        /// Assume the buffer contains a null terminated unicode encoded string.
        /// Write the characters out (as UTF8) to the stream.
        /// \param s is the output stream
        /// \param buffer is the given byte buffer
        /// \param size is the number of bytes in the buffer
        /// \param charsize specifies the encoding (1=UTF8 2=UTF16 4=UTF32)
        /// \return \b true if the byte array contains valid unicode
        public bool writeUnicode(TextWriter s, uint1[] buffer, int4 size, int4 charsize)
        {
            bool bigend = glb.translate.isBigEndian();
            int4 i = 0;
            int4 count = 0;
            int4 skip = charsize;
            while (i < size)
            {
                int4 codepoint = getCodepoint(buffer + i, charsize, bigend, skip);
                if (codepoint < 0) return false;
                if (codepoint == 0) break;      // Terminator
                writeUtf8(s, codepoint);
                i += skip;
                count += 1;
                if (count >= maximumChars)
                    break;
            }
            return true;
        }
    }
}
