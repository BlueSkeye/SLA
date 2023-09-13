using Sla.CORE;
using System.Collections;

namespace Sla.EXTRA
{
    /// \brief An interval map container
    ///
    /// A container for records occupying (possibly overlapping)
    /// intervals.  I.e. a map from a linear ordered domain to
    /// (multiple) records.
    /// The \b recordtype is the main object in the container, it must support:
    ///    - recordtype(inittype,linetype,linetype)   a constructor taking 3 parameters
    ///    - getFirst()     beginning of range
    ///    - getLast()      end of range (inclusive)
    ///    - getSubsort()   retrieve the subsorttype object (see below)
    ///
    /// The \b recordtype must define data-types:
    ///    - linetype
    ///    - subsorttype
    ///    - inittype
    ///
    /// \b linetype is the data-type of elements in the linear domain. It
    /// must support:
    ///    - <,<=            Comparisons
    ///    - ==,!=           Equality
    ///    - + \<constant>   Addition of integers
    ///    - - \<constant>   Subtraction of integers
    ///
    /// \b subsorttype describes how overlapping intervals can be sub-sorted. It
    /// must support:
    ///    - <
    ///    - subsorttype(\b false)  constructor with \b false produces a minimal value
    ///    - subsorttype(\b true)   constructor with \b true produces a maximal value
    ///    - copy constructor
    ///
    /// \b inittype is extra initialization data for the \b recordtype
    ///
    /// The main interval map is implemented as a \e multiset of disjoint sub-ranges mapping
    /// to the \b recordtype objects. After deduping the sub-ranges form the common refinement
    /// of all the possibly overlapping \b recordtype ranges.  A sub-range is duplicated for each
    /// distinct \b recordtype that overlaps that sub-range.  The sub-range multiset is updated
    /// with every insertion or deletion of \b recordtype objects into the container, which
    /// may insert new or delete existing boundary points separating the disjoint subranges.
    internal class rangemap<_recordtype, linetype, subsorttype, inittype>
        where linetype : IComparable<linetype>, IEquatable<linetype>
        where subsorttype : IComparable<subsorttype>
    {
        // Integer data-type defining the linear domain
        //typedef typename _recordtype::linetype linetype;
        // The data-type used for subsorting
        //typedef typename _recordtype::subsorttype subsorttype;
        // The data-type containing initialization data for records
        //typedef typename _recordtype::inittype inittype;

        // The main sub-range iterator data-type
        // typedef PartIterator const_iterator;

        // The underlying multiset of sub-ranges
        private SortedList<AddrRange, AddrRange> tree = new SortedList<AddrRange, AddrRange>(); // Was std::multiset
        // Storage for the actual record objects
        private List<_recordtype> record;
        private IAddable<linetype> linetypeAdder;
        private IRecordTypeInstanciator<_recordtype, inittype, linetype> recordInstanciator;
        private IRangemapSubsortTypeInstantiator<subsorttype> subsortTypeInstatiator;
        private IEqualityComparer<linetype> linetypeEqualityComparer;
        private IComparer<linetype> linetypeComparer;
        
        protected rangemap(IRecordTypeInstanciator<_recordtype, inittype, linetype> recordInstanciator,
            IRangemapSubsortTypeInstantiator<subsorttype> subsortTypeInstatiator,
            IComparer<linetype> linetypeComparer, IAddable<linetype> linetypeAdder)
        {
            this.recordInstanciator = recordInstanciator;
            this.subsortTypeInstatiator = subsortTypeInstatiator;
            this.linetypeComparer = linetypeComparer;
            this.linetypeAdder = linetypeAdder;
        }

        // Added
        internal subsorttype CreateSubsortType(bool value)
        {
            return subsortTypeInstatiator.Create(value);
        }

        internal subsorttype CreateSubsortType(subsorttype value)
        {
            return subsortTypeInstatiator.Create(value);
        }

        internal _recordtype CreateRecord(inittype initdata, linetype a, linetype b)
        {
            return recordInstanciator.CreateRecord(initdata, a, b);
        }

        /// Remove the given partition boundary
        /// All sub-ranges that end with the given boundary point are deleted, and all sub-ranges
        /// that begin with the given boundary point (+1) are extended to cover the deleted sub-range.
        /// This should run in O(k).
        /// \param i is the given boundary point
        /// \param iter points to the first sub-range that ends with the given boundary point
        private void zip(linetype i, /*typename*/ IEnumerator<AddrRange> iter)
        {
            linetype f = iter.Current.first;
            List<AddrRange> toBeErased = new List<AddrRange>();
            while (0 == linetypeComparer.Compare(iter.Current.last, i)) {
                toBeErased.Add(iter.Current);
                iter.MoveNext();
            }
            foreach(AddrRange erased in toBeErased) {
                tree.Remove(erased);
            }
            i = linetypeAdder.IncrementBy(i, 1);
            while (0 == linetypeComparer.Compare(iter.Current.first, i)) {
                iter.Current.first = f;
                if (!iter.MoveNext()) break;
            }
        }

        /// Insert the given partition boundary
        /// All sub-ranges that contain the boundary point will be split into a sub-range
        /// that ends at the boundary point and a sub-range that begins with the boundary point (+1).
        /// This should run in O(k), where k is the number of intervals intersecting the boundary point.
        /// \param i is the given boundary point
        /// \param iter points to the first sub-range containing the boundary point
        private void unzip(linetype i, /*typename*/ IEnumerator<AddrRange> iter)
        {
            /*typename*/ IEnumerator<AddrRange> hint = iter;
            if (0 == linetypeComparer.Compare(iter.Current.last, i))
                // Can't split size 1 (i.e. split already present)
                return;
            linetype f;
            linetype plus1 = linetypeAdder.IncrementBy(i, 1);
            while (0 < linetypeComparer.Compare(iter.Current.first, i)) {
                f = iter.Current.first;
                iter.Current.first = plus1;
                // TODO : Check 
                AddrRange newrange = new AddrRange(i,
                    subsortTypeInstatiator /*iter.Current.subsort*/);
                /*typename*/
                tree.Add(hint.Current, newrange);
                newrange.first = f;
                newrange.a = iter.Current.a;
                newrange.b = iter.Current.b;
                newrange.value = iter.Current.value;
                if (!iter.MoveNext()) break;
            }
        }

        /// Return \b true if the container is empty
        public bool empty() => record.empty();

        /// Clear all records from the container
        public void clear()
        {
            tree.Clear();
            record.Clear();
        }

        /// Beginning of records
        public /*typename*/ IEnumerator<_recordtype> begin_list() => record.GetEnumerator();

        ///// End of records
        //public /*typename*/ IEnumerator<_recordtype> end_list() => record.end();

        /// Beginning of sub-ranges
        internal PartIterator begin() => new PartIterator(tree.begin());

        /// Ending of sub-ranges
        internal PartIterator end() => new PartIterator(tree.end());

        /// \brief Find sub-ranges intersecting the given boundary point
        /// \param point is the given boundary point
        /// \return begin/end iterators over all intersecting sub-ranges
        public Tuple<PartIterator, PartIterator> find(linetype point)
        {
            AddrRange addrrange = new AddrRange(point, subsortTypeInstatiator);
            /*typename*/ IEnumerator<AddrRange> iter1;
            /*typename*/ IEnumerator<AddrRange> iter2;

            iter1 = tree.lower_bound(addrrange);
            // Check for no intersection
            if ((iter1 == tree.end()) || (0 > linetypeComparer.Compare(point, iter1.Current.first)))
                return new Tuple<PartIterator, PartIterator>(new PartIterator(iter1), new PartIterator(iter1));

            AddrRange addrend = new AddrRange(iter1.Current.last,
                subsortTypeInstatiator.Create(true), subsortTypeInstatiator);
            iter2 = tree.upper_bound(addrend);

            return new Tuple<PartIterator, PartIterator>(
                new PartIterator(iter1), new PartIterator(iter2));
        }

        /// \brief Find sub-ranges intersecting given boundary point, and between given \e subsorts
        /// \param point is the given boundary point
        /// \param sub1 is the starting subsort
        /// \param sub2 is the ending subsort
        /// \return begin/end iterators over all intersecting and bounded sub-ranges
        public Tuple<PartIterator, PartIterator> find(linetype point, subsorttype subsort1, subsorttype subsort2)
        {
            AddrRange addrrange = new AddrRange(point, subsort1, subsortTypeInstatiator);
            /*typename*/ IEnumerator<AddrRange> iter2;

            /*typename*/ IEnumerator<AddrRange> iter1 = tree.lower_bound(addrrange);
            if ((iter1 == tree.end()) || (0 > linetypeComparer.Compare(point, iter1.Current.first)))
                return new Tuple<PartIterator, PartIterator>(new PartIterator(iter1),
                    new PartIterator(iter1));

            AddrRange addrend = new AddrRange(iter1.last, subsort2, subsortTypeInstatiator);
            iter2 = tree.upper_bound(addrend);

            return new Tuple<PartIterator, PartIterator>(new PartIterator(iter1),
                new PartIterator(iter2));
        }

        /// \brief Find beginning of sub-ranges that contain the given boundary point
        /// \param point is the given boundary point
        /// \return iterator to first sub-range of intersects the boundary point
        public IEnumerator<AddrRange> find_begin(linetype point)
        {
            AddrRange addrrange = new AddrRange(point, subsortTypeInstatiator);
            /*typename*/ IEnumerator<AddrRange> iter;

            iter = tree.lower_bound(addrrange);
            return iter;
        }

        /// \brief Find ending of sub-ranges that contain the given boundary point
        /// \param point is the given boundary point
        /// \return iterator to first sub-range after that does not intersect the boundary point
        public IEnumerator<AddrRange> find_end(linetype point)
        {
            AddrRange addrend = new AddrRange(point,
                subsortTypeInstatiator.Create(true), subsortTypeInstatiator);
            /*typename*/ IEnumerator<AddrRange> iter;

            iter = tree.upper_bound(addrend);
            if ((iter == tree.end()) || (0 > linetypeComparer.Compare(point, iter.Current.first)))
                return iter;

            // If we reach here, (*iter).last is bigger than point (as per upper_bound) but
            // point >= than iter.Current.Key, i.e. point is contained in the sub-range.
            // So we have to do one more search for first sub-range after the containing sub-range.
            AddrRange addrbeyond = new AddrRange(iter.Current.last,
                subsortTypeInstatiator.Create(true), subsortTypeInstatiator);
            return tree.upper_bound(addrbeyond);
        }

        // \brief Find first record overlapping given interval
        // \param point is the start of interval to test
        // \param end is the end of the interval to test
        // \return iterator to first sub-range of an intersecting record (or \b end)
        // MODIFIED returns the range itself or a null reference.
        public PartIterator find_overlap(linetype point, linetype end)
        {
            AddrRange addrrange = new AddrRange(point, subsortTypeInstatiator);

            // First range where right boundary is equal to or past point
            /*typename*/ PartIterator iter = tree.lower_bound(addrrange);
            if (iter == tree.end()) return iter;
            return (0 < linetypeComparer.Compare(iter.Current.first, end))
                ? iter.Current
                : tree.end();
        }

        /// \brief Insert a new record into the container
        /// \param data is other initialization data for the new record
        /// \param a is the start of the range occupied by the new record
        /// \param b is the (inclusive) end of the range
        /// \return an iterator to the new record
        public /*typename*/ IEnumerator<_recordtype> insert(inittype data, linetype a, linetype b)
        {
            linetype f = a;
            /*typename*/ IEnumerator<_recordtype> liter;
            /*typename*/ IEnumerator<AddrRange> low = tree.lower_bound(
                             new AddrRange(f, subsortTypeInstatiator));

            if (low != tree.end()) {
                if (0 > linetypeComparer.Compare(low.Current.first, f))
                    // Check if left boundary refines existing partition
                    // If so do the refinement
                    unzip(f = linetypeAdder.DecrementBy(f, 1), low);
            }
            
            _recordtype newItem = CreateRecord(data, a, b);
            record.Insert(0, newItem);
            liter = record.GetEnumerator();

            AddrRange addrrange = new AddrRange(b, subsortTypeInstatiator);
            addrrange.a = a;
            addrrange.b = b;
            addrrange.value = liter;
            /*typename*/ IEnumerator<AddrRange> spot = tree.lower_bound(addrrange);
            // Where does the new record go in full list, insert it
            record.splice((spot == tree.end())
                ? record.end()
                : spot.Current.value, record, liter);

            while ((low != tree.end()) && (0 < linetypeComparer.Compare(low.Current.first, b))) {
                if (0 < linetypeComparer.Compare(f, low.Current.last)) {
                    // Do we overlap at all
                    if (0 > linetypeComparer.Compare(f, low.Current.first)) {
                        // Assume the hint makes this insert an O(1) op
                        addrrange.first = f;
                        addrrange.last = linetypeAdder.DecrementBy(low.Current.first, 1);
                        tree.insert(low, addrrange);
                        f = low.Current.first;
                    }
                    if (0 < linetypeComparer.Compare(low.Current.last, b)) {
                        // Insert as much of interval as we can
                        addrrange.first = f;
                        addrrange.last = low.Current.last;
                        tree.insert(low, addrrange);
                        if (0 == linetypeComparer.Compare(low.Current.last, b))
                            // Did we manage to insert it all
                            break;
                        f = linetypeAdder.IncrementBy(low.Current.last, 1);
                    }
                    else if (0 > linetypeComparer.Compare(b, low.Current.last)) {
                        // We can insert everything left, but must refine
                        unzip(b, low);
                        break;
                    }
                }
                ++low;
            }
            if (0 < linetypeComparer.Compare(f, b)) {
                addrrange.first = f;
                addrrange.last = b;
                tree.Add(addrrange, addrrange);
            }
            return liter;
        }

        /// \brief Erase a given record from the container
        /// \param v is the iterator to the record to be erased
        public void erase(/*typename*/ IEnumerator<_recordtype> v)
        {
            linetype a = v.Current.getFirst();
            linetype b = v.Current.getLast();
            bool leftsew = true;
            bool rightsew = true;
            bool rightoverlap = false;
            bool leftoverlap = false;
            IEnumerator<AddrRange> low = tree.lower_bound(
                new AddrRange(a, subsortTypeInstatiator));
            IEnumerator<AddrRange> uplow = low;

            linetype aminus1 = linetypeAdder.DecrementBy(a, 1);
            while (uplow != tree.begin()) {
                --uplow;
                if (0 != linetypeComparer.Compare(uplow.Current.last, aminus1)) break;
                if (0 == linetypeComparer.Compare(uplow.Current.b, aminus1)) {
                    // Still a split between a-1 and a
                    leftsew = false;
                    break;
                }
            }
            do {
                if (low.Current.value == v) {
                    tree.erase(low.Current);
                }
                else {
                    if (0 > linetypeComparer.Compare(low.Current.a, a))
                        leftoverlap = true; // a splits somebody else
                    else if (0 == linetypeComparer.Compare(low.Current.a, a))
                        leftsew = false;    // Somebody else splits at a (in addition to v)
                    if (0 > linetypeComparer.Compare(b, low.Current.b))
                        rightoverlap = true;    // b splits somebody else
                    else if (0 == linetypeComparer.Compare(low.Current.b, b))
                        rightsew = false;   // Somebody else splits at b (in addition to v)
                }
            } while (low.MoveNext() && (0 < linetypeComparer.Compare(low.Current.first, b)));
            if (low != tree.end()) {
                if (linetypeAdder.DecrementBy(low.Current.a, 1).Equals(b))
                    rightsew = false;
            }
            if (leftsew && leftoverlap)
                zip(linetypeAdder.DecrementBy(a, 1), tree.lower_bound(
                    new AddrRange(linetypeAdder.DecrementBy(a, 1), subsortTypeInstatiator)));
            if (rightsew && rightoverlap)
                zip(b, tree.lower_bound(new AddrRange(b, subsortTypeInstatiator)));
            record.Remove(v.Current);
        }

        // \brief Erase a record given an iterator
        public void erase(IEnumerator<AddrRange> iter)
        {
            erase(iter.getValueIter());
        }

        /// \brief The internal \e sub-range object for the interval map
        /// It defines a disjoint range within the common refinement of all ranges
        /// in the container. It also knows about its containing range and \b recordtype.
        internal class AddrRange
        {
            //friend class rangemap<_recordtype>;
            //friend class PartIterator;
            // Start of the disjoint sub-range
            internal /*mutable*/ linetype first;
            // End of the disjoint sub-range
            internal linetype last;
            // Start of full range occupied by the entire \b recordtype
            internal /*mutable*/ linetype a;
            // End of full range occupied by the entire \b recordtype
            internal /*mutable*/ linetype b;
            // How \b this should be sub-sorted
            internal /*mutable*/ subsorttype subsort;
            // How \b this should be sub-sorted
            internal /*mutable*/ /*typename*/ IEnumerator<_recordtype> value;

            /// (Partial) constructor
            internal AddrRange(linetype l,
                IRangemapSubsortTypeInstantiator<subsorttype> subsorttypeInstantiator)
            {
                subsort = subsorttypeInstantiator.Create(false);
                last = l;
            }

            /// (Partial) constructor given a subsort
            internal AddrRange(linetype l, subsorttype s,
                IRangemapSubsortTypeInstantiator<subsorttype> subsorttypeInstantiator)
            {
                subsort = subsorttypeInstantiator.Create(s);
                last = l;
            }

            // Comparison method based on ending boundary point
            public static bool operator <(AddrRange op1, AddrRange op2)
            {
                return (op1.last.Equals(op2.last))
                    ? (0 > op1.subsort.CompareTo(op2.subsort))
                    : (0 > op1.last.CompareTo(op2.last));
            }

            public static bool operator >(AddrRange op1, AddrRange op2)
            {
                throw new NotImplementedException();
            }

            /// Retrieve the \b recordtype
            public /*typename*/ IEnumerator<_recordtype> getValue() => value;
        }

        // \brief An iterator into the interval map container
        // This is really an iterator to the underlying multiset, but dereferencing it returns
        // the \b recordtype. Iteration occurs over the disjoint sub-ranges, thus the same
        // \b recordtype may be visited multiple times by the iterator, depending on how much
        // it overlaps other \b recordtypes. The sub-ranges are sorted in linear order, then
        // depending on the \b subsorttype.
        public class PartIterator : IEnumerator<_recordtype>
        {
            /// The underlying multiset iterator
            private /*typename*/ IEnumerator<AddrRange> iter;

            public PartIterator()
            {
            }

            // Construct given iterator
            public PartIterator(/*typename*/ IEnumerator<AddrRange> i)
            {
                iter = i;
            }

            // Dereference to the \b recordtype object
            public _recordtype Current => iter.Current;

            object IEnumerator.Current => this.Current;

            public bool MoveNext()
            {
                throw new NotImplementedException();
            }

            // Pre-increment the iterator
            public static PartIterator? operator ++(PartIterator iterator)
            {
                return iterator.iter.MoveNext() ? iterator : null;
            }

            ///// Post-increment the iterator
            //public static PartIterator operator ++(int i)
            //{
            //    PartIterator orig = new PartIterator(iter);
            //    ++iter;
            //    return orig;
            //}

            // Pre-decrement the iterator
            public static PartIterator? operator --(PartIterator iterator)
            {
                throw new NotImplementedException();
                //--iter;
                //return iterator;
            }

            ///// Post-decrement the iterator
            //public static PartIterator operator --(int i)
            //{
            //    throw new NotImplementedException();
            //    //PartIterator orig = new PartIterator(iter);
            //    //--iter;
            //    //return orig;
            //}

            // Assign to the iterator
            // TODO : Find assignment use and duplicate in a specific method.
            //public static PartIterator operator=(PartIterator op1, PartIterator op2)
            //{
            //    op1.iter = op2.iter;
            //    return op1;
            //}

            // Test equality of iterators
            public static bool operator ==(PartIterator op1, PartIterator op2)
            {
                return (op1.iter == op2.iter);
            }

            // Test inequality of iterators
            public static bool operator !=(PartIterator op1, PartIterator op2)
            {
                return (op1.iter != op2.iter);
            }

            // Get the \b recordtype iterator
            public /*typename*/ IEnumerator<_recordtype> getValueIter() => iter;

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        protected class AddressLinetypeAdder : IAddable<Address>
        {
            internal static readonly AddressLinetypeAdder Instance =
                new AddressLinetypeAdder();

            private AddressLinetypeAdder()
            {
            }

            public Address DecrementBy(Address initialValue, int decrementBy)
            {
                return (initialValue - decrementBy);
            }

            public Address IncrementBy(Address initialValue, int incrementBy)
            {
                return (initialValue + incrementBy);
            }
        }

        protected class UInt64LinetypeAdder : IAddable<ulong>
        {
            internal static readonly UInt64LinetypeAdder Instance =
                new UInt64LinetypeAdder();

            private UInt64LinetypeAdder()
            {
            }

            public ulong DecrementBy(ulong initialValue, int decrementBy)
            {
                return (0 <= decrementBy)
                    ? initialValue - (uint)decrementBy
                    : initialValue + (uint)(-decrementBy);
            }

            public ulong IncrementBy(ulong initialValue, int incrementBy)
            {
                return (0 <= incrementBy)
                    ? initialValue + (uint)incrementBy
                    : initialValue - (uint)(-incrementBy);
            }
        }
    }
}
