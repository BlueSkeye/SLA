using Sla.CORE;
using System.Collections;

namespace Sla.DECCORE
{
    /// \brief Describes a (register) storage location and the ways it might be split into lanes
    internal class LanedRegister : IEnumerable<int>
    {
        /// \brief Class for iterating over possible lane sizes
        private class LanedIterator : IEnumerator<int>
        {
            private bool _completed;
            private bool _disposed;
            // Current lane size
            private int size;
            // Collection being iterated over
            private uint mask;

            internal LanedIterator(LanedRegister lanedR)
            {
                size = 0;
                mask = lanedR.sizeBitMask;
                normalize();
            }

            internal LanedIterator()
            {
                size = -1;
                mask = 0;
                _completed = false;
            }

            ///// Dereference operator
            //public static int operator *() => size;
            public int Current
            {
                get
                {
                    AssertNotDisposed();
                    return size;
                }
            }

            object IEnumerator.Current => this.Current;

            private void AssertNotDisposed()
            {
                if (_disposed) throw new InvalidOperationException();
            }

            public void Dispose()
            {
                _disposed = true;
            }

            /// Preincrement operator
            public bool MoveNext()
            {
                AssertNotDisposed();
                size += 1;
                normalize();
                return !_completed;
            }
            //public static LanedIterator operator ++(LanedIterator item)
            //{
            //    item.size += 1;
            //    item.normalize();
            //    return item;
            //}

            // Normalize the iterator, after increment or initialization
            private void normalize()
            {
                uint flag = 1;
                flag <<= size;
                while (flag <= mask) {
                    // Found a valid lane size
                    if ((flag & mask) != 0) return;
                    size += 1;
                    flag <<= 1;
                }
                // Indicate ending iterator
                size = -1;
                _completed = true;
            }

            public void Reset()
            {
                AssertNotDisposed();
                throw new NotSupportedException();
            }

            // Assignment
            // TODO : Find assignment use and duplicate in a specific method.
            //public static LanedIterator operator=(LanedIterator op1, LanedIterator op2)
            //{
            //    op1.size = op2.size;
            //    op1.mask = op2.mask;
            //    return op1;
            //}

            // Equal operator
            public static bool operator ==(LanedIterator op1, LanedIterator op2)
            {
                op1.AssertNotDisposed();
                op2.AssertNotDisposed();
                return (op1.size == op2.size);
            }

            // Not-equal operator
            public static bool operator !=(LanedIterator op1, LanedIterator op2)
                => !(op1.size == op2.size); 
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
            ElementId elemId = decoder.openElement(ElementId.ELEM_REGISTER);
            string laneSizes = string.Empty;
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
                    // To the end of the string
                    value = laneSizes.Substring(pos);
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

        ///// Starting iterator over possible lane sizes
        //public const_iterator begin() => new LanedIterator(this);

        ///// Ending iterator over possible lane sizes
        //public const_iterator end() => new LanedIterator();

        public IEnumerator<int> GetEnumerator()
        {
            return new LanedIterator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
