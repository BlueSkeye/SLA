using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A disjoint set of Ranges, possibly across multiple address spaces
    /// This is a container for addresses. It maintains a disjoint list of Ranges
    /// that cover all the addresses in the container.  Ranges can be inserted
    /// and removed, but overlapping/adjacent ranges will get merged.
    public class RangeList : IEnumerable<Range>
    {
        /// The sorted list of Range objects
        private SortedSet<Range> tree;

        /// Copy constructor
        public RangeList(ref RangeList op2)
        {
            tree = op2.tree;
        }

        /// Construct an empty container
        public RangeList()
        {
            tree = new SortedSet<Range>();
        }

        /// Clear \b this container to empty
        public void clear()
        {
            tree.Clear();
        }

        ///< Return \b true if \b this is empty
        public bool empty() => (0 == tree.Count);

        /// Get iterator to beginning Range
        public IEnumerator<Range> begin() => tree.GetEnumerator();

        ///// Get iterator to ending Range
        //public set<Range>::const_iterator end()
        //{
        //    return tree.end();
        //}

        /// Return the number of Range objects in container
        public int numRanges()
        {
            return tree.Count;
        }
    
        /// Get the first Range
        /// \return the first contiguous range of addresses or NULL if empty
        public Range? getFirstRange()
        {
            if (this.empty()) {
                return null;
            }
            return tree.First();
        }

        /// Get the last Range
        ///\return the last contiguous range of addresses or NULL if empty
        public Range? getLastRange()
        {
            if (this.empty()) {
                return null;
            }
            return tree.Last();
        }

        /// Get the last Range viewing offsets as signed
        /// Treating offsets with their high-bits set as coming \e before
        /// offset where the high-bit is clear, return the last/latest contiguous
        /// Range within the given address space
        /// \param spaceid is the given address space
        /// \return indicated Range or NULL if empty
        public Range? getLastSignedRange(AddrSpace spaceid)
        {
            // Maximal signed value
            ulong midway = spaceid.getHighest() / 2;
            Range range = new Range(spaceid, midway, midway);
            // First element greater than -range- (should be MOST negative)
            bool firstRange;
            IEnumerator<Range> next;
            Range candidate = tree.BeforeUpperBound(range, out firstRange, out next);
            if (!firstRange) {
                if (candidate.getSpace() == spaceid) {
                    return candidate;
                }
            }

            // If there were no "positive" ranges, search for biggest negative range
            range = new Range(spaceid, spaceid.getHighest(), spaceid.getHighest());
            candidate = tree.BeforeUpperBound(range, out firstRange, out next);
            if (!firstRange) {
                if (candidate.getSpace() == spaceid) {
                    return candidate;
                }
            }
            return null;
        }

        ///< Get Range containing the given byte
        /// If \b this RangeList contains the specific address (spaceid,offset), return it
        /// \return the containing Range or NULL
        public Range? getRange(AddrSpace spaceid, ulong offset)
        {
            if (this.empty()) {
                return null;
            }
            // iter = first range with its first > offset
            bool firstRange;
            IEnumerator<Range> next;
            Range candidate = tree.BeforeUpperBound(new Range(spaceid, offset, offset),
                out firstRange, out next);
            if (firstRange) {
                return null;
            }
            // Set iter to last range with range.first <= offset
            if (candidate.spc != spaceid) {
                return null;
            }
            if (candidate.last >= offset) {
                return candidate;
            }
            return null;
        }

        /// Insert a range of addresses
        /// Insert a new Range merging as appropriate to maintain the disjoint cover
        /// \param spc is the address space containing the new range
        /// \param first is the offset of the first byte in the new range
        /// \param last is the offset of the last byte in the new range
        public void insertRange(AddrSpace spc, ulong first, ulong last)
        {
            // we must have iter1.first > first
            IEnumerator<Range> next;
            bool firstRange;
            Range candidate = tree.BeforeUpperBound(new Range(spc, first, first),
                out firstRange, out next);

            // Set iter1 to first range with range.last >=first
            // It is either current iter1 or the one before
            if (!firstRange) {
                if ((candidate.spc != spc) || (candidate.last < first)) {
                    candidate = next.Current;
                }
            }

            // Set iter2 to first range with range.first > last
            IEnumerator<Range> next2;
            Range candidate2 = tree.BeforeUpperBound(new Range(spc, last, last),
                out firstRange, out next2);
            List<Range> eraseList = new List<Range>();

            while (candidate != candidate2) {
                if (candidate.first < first) {
                    first = candidate.first;
                }
                if (candidate.last > last) {
                    last = candidate.last;
                }
                eraseList.Add(candidate);
                if (!next.MoveNext()) {
                    throw new BugException();
                }
                candidate = next.Current;
            }
            foreach(Range erased in eraseList) {
                tree.Remove(erased);
            }
            tree.Add(new Range(spc, first, last));
        }
    
        /// Remove a range of addresses
        /// Remove/narrow/split existing Range objects to eliminate the indicated addresses
        /// while still maintaining a disjoint cover.
        /// \param spc is the address space of the address range to remove
        /// \param first is the offset of the first byte of the range
        /// \param last is the offset of the last byte of the range
        public void removeRange(AddrSpace spc, ulong first, ulong last)
        {
            // remove a range
            if (this.empty()) {
                // Nothing to do
                return;
            }

            // we must have iter1.first > first
            bool isFirst;
            IEnumerator<Range> next;
            Range candidate = tree.BeforeUpperBound(new Range(spc, first, first),
                out isFirst, out next);

            // Set iter1 to first range with range.last >=first
            // It is either current iter1 or the one before
            if (!isFirst) {
                if ((candidate.spc != spc) || (candidate.last < first)) {
                    candidate = next.Current;
                }
            }

            // Set iter2 to first range with range.first > last
            bool isFirst2;
            IEnumerator<Range> next2;
            Range candidate2 = tree.BeforeUpperBound(new Range(spc, last, last),
                out isFirst2, out next2);
            List<Range> eraseList = new List<Range>();
            List<Range> addList = new List<Range>();
            while (candidate != candidate2) {
                ulong a = candidate.first;
                ulong b = candidate.last;
                eraseList.Add(candidate);
                if (!next.MoveNext()) {
                    throw new BugException();
                }
                if (a < first) {
                    addList.Add(new Range(spc, a, first - 1));
                }
                if (b > last) {
                    addList.Add(new Range(spc, last + 1, b));
                }
            }
            foreach(Range erased in eraseList) {
                tree.Remove(erased);
            }
            foreach(Range added in addList) {
                tree.Add(added);
            }
        }
    
        ///< Merge another RangeList into \b this
        public void merge(RangeList op2)
        {
            // Merge -op2- into this rangelist
            IEnumerator<Range> iter1 = op2.tree.GetEnumerator();
            if (iter1.MoveNext()) {
                do {
                    Range range = iter1.Current;
                    insertRange(range.spc, range.first, range.last);
                } while (iter1.MoveNext());
            }
        }

        ///< Check containment an address range
        /// Make sure indicated range of addresses is \e contained in \b this RangeList
        /// \param addr is the first Address in the target range
        /// \param size is the number of bytes in the target range
        /// \return \b true is the range is fully contained by this RangeList
        public bool inRange(Address addr,int size)
        {
            if (addr.isInvalid()) {
                // We don't really care
                return true;
            }
            if (this.empty()) {
                return false;
            }
            // iter = first range with its first > addr
            bool isFirst;
            IEnumerator<Range> next;
            Range candidate = tree.BeforeUpperBound(
                new Range(addr.getSpace(),addr.getOffset(),addr.getOffset()),
                out isFirst, out next);
            if (isFirst) {
                return false;
            }
            // Set iter to last range with range.first <= addr
            //  if (iter == tree.end())   // iter can't be end if non-empty
            //    return false;
            if (candidate.spc != addr.getSpace()) {
                return false;
            }
            return (candidate.last >= addr.getOffset() + (ulong)(size - 1));
        }

        /// Find size of biggest Range containing given address
        /// Return the size of the biggest contiguous sequence of addresses in
        /// \b this RangeList which contain the given address
        /// \param addr is the given address
        /// \param maxsize is the large range to consider before giving up
        /// \return the size (in bytes) of the biggest range
        public ulong longestFit(Address addr,ulong maxsize)
        {
            if (addr.isInvalid()) {
                return 0;
            }
            if (0 == tree.Count) {
                return 0;
            }
            // iter = first range with its first > addr
            ulong offset = addr.getOffset();
            bool first;
            IEnumerator<Range> next;
            Range iter = tree.BeforeUpperBound(new Range(addr.getSpace(),offset,offset),
                out first, out next);
            if (first) {
                return 0;
            }
            // Set iter to last range with range.first <= addr
            ulong sizeres = 0;
            if (iter.last < offset) {
                return sizeres;
            }
            do {
                if (iter.spc != addr.getSpace()) {
                    break;
                }
                if (iter.first > offset) {
                    break;
                }
                // Size extends to end of range
                sizeres += (iter.last + 1 - offset);
                // Try to chain on the next range
                offset = iter.last + 1;
                if (sizeres >= maxsize) {
                    // Don't bother if past maxsize
                    break;
                }
                // Next range in the chain
            } while (next.MoveNext()) ;
            return sizeres;
        }

        ///< Print a description of \b this RangeList to stream
        /// Print a one line description of each disjoint Range making up \b this RangeList
        /// \param s is the output stream
        public void printBounds(TextWriter s)
        {
            if (this.empty()) {
                s.WriteLine("all");
            }
            else {
                foreach(Range scannedRange in tree) {
                    scannedRange.printBounds(s);
                    s.WriteLine();
                }
            }
        }

        /// Encode \b this RangeList to a stream
        /// Encode \b this as a \<rangelist> element
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_RANGELIST);
            foreach(Range scannedRange in tree) {
                scannedRange.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_RANGELIST);
        }

        /// Decode \b this RangeList from a \<rangelist> element
        /// Recover each individual disjoint Range for \b this RangeList.
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_RANGELIST);
            while (0 != decoder.peekElement()) {
                Range range = new Range();
                range.decode(decoder);
                tree.Add(range);
            }
            decoder.closeElement(elemId);
        }

        public IEnumerator<Range> GetEnumerator()
        {
            return begin();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
