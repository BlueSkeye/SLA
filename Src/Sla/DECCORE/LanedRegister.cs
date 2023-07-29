using ghidra;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Describes a (register) storage location and the ways it might be split into lanes
    internal class LanedRegister
    {
        /// \brief Class for iterating over possible lane sizes
        public class LanedIterator
        {
            /// Current lane size
            private int4 size;
            /// Collection being iterated over
            private uint4 mask;

            /// Normalize the iterator, after increment or initialization
            private void normalize()
            {
                uint4 flag = 1;
                flag <<= size;
                while (flag <= mask)
                {
                    if ((flag & mask) != 0) return; // Found a valid lane size
                    size += 1;
                    flag <<= 1;
                }
                size = -1;      // Indicate ending iterator
            }

            public LanedIterator(LanedRegister lanedR)
            {
                size = 0;
                mask = lanedR.sizeBitMask;
                normalize();
            }

            public LanedIterator()
            {
                size = -1;
                mask = 0;
            }

            /// Preincrement operator
            public static LanedIterator operator++()
            {
                size += 1;
                normalize();
                return *this;
            }

            /// Dereference operator
            public static int4 operator *() => size;

            ///< Assignment
            public static LanedIterator operator=(LanedIterator op2)
            {
                size = op2.size;
                mask = op2.mask;
                return *this;
            }

            /// Equal operator
            public static bool operator ==(LanedIterator op2) => (size == op2.size);

            /// Not-equal operator
            public static bool operator !=(LanedIterator op2) => (size != op2.size); 
        }
        /// Iterator over possible lane sizes for this register
        // typedef LanedIterator const_iterator;

        /// Size of the whole register
        private int4 wholeSize;
        /// A 1-bit for every permissible lane size
        private uint4 sizeBitMask;

        /// Constructor for use with decode
        public LanedRegister()
        {
            wholeSize = 0;
            sizeBitMask = 0;
        }

        public LanedRegister(int4 sz, uint4 mask)
        {
            wholeSize = sz;
            sizeBitMask = mask;
        }

        /// Parse \<register> elements for lane sizes
        /// Parse any vector lane sizes.
        /// \param decoder is the stream decoder
        /// \return \b true if the XML description provides lane sizes
        public bool decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_REGISTER);
            string laneSizes;
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_VECTOR_LANE_SIZES)
                {
                    laneSizes = decoder.readString();
                    break;
                }
            }
            if (laneSizes.empty())
            {
                decoder.closeElement(elemId);
                return false;
            }
            decoder.rewindAttributes();
            VarnodeData storage;
            storage.space = (AddrSpace*)0;
            storage.decodeFromAttributes(decoder);
            decoder.closeElement(elemId);
            wholeSize = storage.size;
            sizeBitMask = 0;
            string::size_type pos = 0;
            while (pos != string::npos)
            {
                string::size_type nextPos = laneSizes.find(',', pos);
                string value;
                if (nextPos == string::npos)
                {
                    value = laneSizes.substr(pos);  // To the end of the string
                    pos = nextPos;
                }
                else
                {
                    value = laneSizes.substr(pos, (nextPos - pos));
                    pos = nextPos + 1;
                    if (pos >= laneSizes.size())
                        pos = string::npos;
                }
                istringstream s(value);
                s.unsetf(ios::dec | ios::hex | ios::oct);
                int4 sz = -1;
                s >> sz;
                if (sz < 0 || sz > 16)
                    throw new LowlevelError("Bad lane size: " + value);
                addLaneSize(sz);
            }
            return true;
        }

        /// Get the size in bytes of the whole laned register
        public int4 getWholeSize() => wholeSize;

        /// Get the bit mask of possible lane sizes
        public uint4 getSizeBitMask() => sizeBitMask;

        /// Add a new \e size to the allowed list
        public void addLaneSize(int4 size)
        {
            sizeBitMask |= ((uint4)1 << size);
        }

        /// Is \e size among the allowed lane sizes
        public bool allowedLane(int4 size) => (((sizeBitMask >> size) &1) != 0);

        /// Starting iterator over possible lane sizes
        public const_iterator begin() => new LanedIterator(this);

        /// Ending iterator over possible lane sizes
        public const_iterator end() => new LanedIterator();
    }
}
