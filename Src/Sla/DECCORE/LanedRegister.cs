using Sla.CORE;
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
            private int size;
            /// Collection being iterated over
            private uint mask;

            /// Normalize the iterator, after increment or initialization
            private void normalize()
            {
                uint flag = 1;
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
            public static LanedIterator operator++(LanedIterator item)
            {
                item.size += 1;
                item.normalize();
                return item;
            }

            /// Dereference operator
            public static int operator *() => size;

            ///< Assignment
            public static LanedIterator operator=(LanedIterator op1, LanedIterator op2)
            {
                op1.size = op2.size;
                op1.mask = op2.mask;
                return this;
            }

            /// Equal operator
            public static bool operator ==(LanedIterator op1, LanedIterator op2) => (op1.size == op2.size);

            /// Not-equal operator
            public static bool operator !=(LanedIterator op1, LanedIterator op2) => (op1.size != op2.size); 
        }
        /// Iterator over possible lane sizes for this register
        // typedef LanedIterator const_iterator;

        /// Size of the whole register
        private int wholeSize;
        /// A 1-bit for every permissible lane size
        private uint sizeBitMask;

        /// Constructor for use with decode
        public LanedRegister()
        {
            wholeSize = 0;
            sizeBitMask = 0;
        }

        public LanedRegister(int sz, uint mask)
        {
            wholeSize = sz;
            sizeBitMask = mask;
        }

        /// Parse \<register> elements for lane sizes
        /// Parse any List lane sizes.
        /// \param decoder is the stream decoder
        /// \return \b true if the XML description provides lane sizes
        public bool decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_REGISTER);
            string laneSizes;
            while(true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_VECTOR_LANE_SIZES) {
                    laneSizes = decoder.readString();
                    break;
                }
            }
            if (laneSizes.empty()) {
                decoder.closeElement(elemId);
                return false;
            }
            decoder.rewindAttributes();
            VarnodeData storage = VarnodeData.decodeFromAttributes(decoder);
            storage.space = (AddrSpace)null;
            decoder.closeElement(elemId);
            wholeSize = (int)storage.size;
            sizeBitMask = 0;
            int pos = 0;
            while (-1 != pos) {
                int nextPos = laneSizes.IndexOf(',', pos);
                string value;
                if (-1 == nextPos) {
                    value = laneSizes.Substring(pos);  // To the end of the string
                    pos = nextPos;
                }
                else {
                    value = laneSizes.Substring(pos, (nextPos - pos));
                    pos = nextPos + 1;
                    if (pos >= laneSizes.Length)
                        pos = -1;
                }
                int sz = -1;
                sz = int.Parse(value);
                if (sz < 0 || sz > 16)
                    throw new LowlevelError("Bad lane size: " + value);
                addLaneSize(sz);
            }
            return true;
        }

        /// Get the size in bytes of the whole laned register
        public int getWholeSize() => wholeSize;

        /// Get the bit mask of possible lane sizes
        public uint getSizeBitMask() => sizeBitMask;

        /// Add a new \e size to the allowed list
        public void addLaneSize(int size)
        {
            sizeBitMask |= ((uint)1 << size);
        }

        /// Is \e size among the allowed lane sizes
        public bool allowedLane(int size) => (((sizeBitMask >> size) &1) != 0);

        /// Starting iterator over possible lane sizes
        public const_iterator begin() => new LanedIterator(this);

        /// Ending iterator over possible lane sizes
        public const_iterator end() => new LanedIterator();
    }
}
