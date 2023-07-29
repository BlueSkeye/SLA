using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    internal class rangemap<_recordtype>
    {
        //typedef typename _recordtype::linetype linetype;  ///< Integer data-type defining the linear domain
        //typedef typename _recordtype::subsorttype subsorttype;  ///< The data-type used for subsorting
        //typedef typename _recordtype::inittype inittype;    ///< The data-type containing initialization data for records
  /// \brief The internal \e sub-range object for the interval map
  ///
  /// It defines a disjoint range within the common refinement of all ranges
  /// in the container. It also knows about its containing range and \b recordtype.
private class AddrRange
        {
            //friend class rangemap<_recordtype>;
            //friend class PartIterator;
            private /*mutable*/ linetype first; ///< Start of the disjoint sub-range
            private linetype last;      ///< End of the disjoint sub-range
            private /*mutable*/ linetype a;     ///< Start of full range occupied by the entire \b recordtype
            private /*mutable*/ linetype b;     ///< End of full range occupied by the entire \b recordtype
            private /*mutable*/ subsorttype subsort;    ///< How \b this should be sub-sorted
            private /*mutable*/ typename std::list<_recordtype>::iterator value;    ///< Iterator pointing at the actual \b recordtype

            /// (Partial) constructor
            private AddrRange(linetype l)
            {
                subsort = new subsorttype(false);
                last = l;
            }

            /// (Partial) constructor given a subsort
            private AddrRange(linetype l, subsorttype s)
            {
                subsort = new subsorttype(s);
                last = l;
            }

            ///< Comparison method based on ending boundary point
            public static bool operator <(AddrRange op1, AddrRange op2)
            {
                if (last != op2.last) return (last < op2.last);
                return (subsort < op2.subsort);
            }

            /// Retrieve the \b recordtype
            public typename std::list<_recordtype>::iterator getValue() => value; 
        }

        /// \brief An iterator into the interval map container
        ///
        /// This is really an iterator to the underlying multiset, but dereferencing it returns the
        /// \b recordtype.  Iteration occurs over the disjoint sub-ranges, thus the same \b recordtype
        /// may be visited multiple times by the iterator, depending on how much it overlaps other
        /// \b recordtypes. The sub-ranges are sorted in linear order, then depending on the \b subsorttype.
        public class PartIterator
        {
            /// The underlying multiset iterator
            private typename std::multiset<AddrRange>::const_iterator iter;

            public PartIterator()
            {
            }

            /// Construct given iterator
            public PartIterator(typename std::multiset<AddrRange>::const_iterator i)
            {
                iter = i;
            }

            /// Dereference to the \b recordtype object
            public static _recordtype operator *()
            {
                return *(*iter).value;
            }

            /// Pre-increment the iterator
            public static PartIterator operator ++()
            {
                ++iter;
                return *this;
            }

            /// Post-increment the iterator
            public static PartIterator operator ++(int i)
            {
                PartIterator orig(iter); ++iter; return orig;
            }

            /// Pre-decrement the iterator
            public static PartIterator operator --()
            {
                --iter;
                return *this;
            }

            /// Post-decrement the iterator
            public static PartIterator operator --(int i)
            {
                PartIterator orig(iter); --iter; return orig;
            }

            /// Assign to the iterator
            public static PartIterator operator=(PartIterator op2)
            {
                iter = op2.iter; return *this;
            }

            /// Test equality of iterators
            public static bool operator ==(PartIterator op1, PartIterator op2)
            {
                return (iter == op2.iter);
            }

            /// Test inequality of iterators
            public static bool operator !=(PartIterator op1, PartIterator op2)
            {
                return (iter != op2.iter);
            }

            public typename std::list<_recordtype>::iterator getValueIter()  => (* iter).getValue();                ///< Get the \b recordtype iterator
        }

        // typedef PartIterator const_iterator;		///< The main sub-range iterator data-type

        private std::multiset<AddrRange> tree;    ///< The underlying multiset of sub-ranges
        private List<_recordtype> record;  ///< Storage for the actual record objects

        /// Remove the given partition boundary
        /// All sub-ranges that end with the given boundary point are deleted, and all sub-ranges
        /// that begin with the given boundary point (+1) are extended to cover the deleted sub-range.
        /// This should run in O(k).
        /// \param i is the given boundary point
        /// \param iter points to the first sub-range that ends with the given boundary point
        private void zip(linetype i, typename std::multiset<AddrRange>::iterator iter)
        {
            linetype f = (*iter).first;
            while ((*iter).last == i)
                tree.erase(iter++);
            i = i + 1;
            while ((iter != tree.end()) && ((*iter).first == i))
            {
                (*iter).first = f;
                ++iter;
            }
        }

        /// Insert the given partition boundary
        /// All sub-ranges that contain the boundary point will be split into a sub-range
        /// that ends at the boundary point and a sub-range that begins with the boundary point (+1).
        /// This should run in O(k), where k is the number of intervals intersecting the boundary point.
        /// \param i is the given boundary point
        /// \param iter points to the first sub-range containing the boundary point
        private void unzip(linetype i, typename std::multiset<AddrRange>::iterator iter)
        {
            typename std::multiset<AddrRange>::iterator hint = iter;
            if ((*iter).last == i) return; // Can't split size 1 (i.e. split already present)
            linetype f;
            linetype plus1 = i + 1;
            while ((iter != tree.end()) && ((*iter).first <= i))
            {
                f = (*iter).first;
                (*iter).first = plus1;
                typename std::multiset<AddrRange>::iterator newiter;
                newiter = tree.insert(hint, AddrRange(i, (*iter).subsort));
                const AddrRange &newrange(*newiter);
                newrange.first = f;
                newrange.a = (*iter).a;
                newrange.b = (*iter).b;
                newrange.value = (*iter).value;
                ++iter;
            }
        }

        /// Return \b true if the container is empty
        public bool empty() => record.empty();

        /// Clear all records from the container
        public void clear()
        {
            tree.clear();
            record.clear();
        }

        /// Beginning of records
        public typename std::list<_recordtype>::const_iterator begin_list() => record.begin();

        /// End of records
        public typename std::list<_recordtype>::const_iterator end_list() => record.end();

        /// Beginning of records
        public typename std::list<_recordtype>::iterator begin_list() => record.begin();

        /// End of records
        public typename std::list<_recordtype>::iterator end_list() => record.end();

        /// Beginning of sub-ranges
        public const_iterator begin() => PartIterator(tree.begin());

        /// Ending of sub-ranges
        public const_iterator end() => PartIterator(tree.end());

        /// \brief Find sub-ranges intersecting the given boundary point
        /// \param point is the given boundary point
        /// \return begin/end iterators over all intersecting sub-ranges
        public std::pair<const_iterator, const_iterator> find(linetype a)
        {
            AddrRange addrrange(point);
            typename std::multiset<AddrRange>::const_iterator iter1, iter2;

            iter1 = tree.lower_bound(addrrange);
            // Check for no intersection
            if ((iter1 == tree.end()) || (point < (*iter1).first))
                return std::pair<PartIterator, PartIterator>(PartIterator(iter1), PartIterator(iter1));

            AddrRange addrend((* iter1).last, subsorttype(true));
            iter2 = tree.upper_bound(addrend);

            return std::pair<PartIterator, PartIterator>(PartIterator(iter1), PartIterator(iter2));
        }

        /// \brief Find sub-ranges intersecting given boundary point, and between given \e subsorts
        /// \param point is the given boundary point
        /// \param sub1 is the starting subsort
        /// \param sub2 is the ending subsort
        /// \return begin/end iterators over all intersecting and bounded sub-ranges
        public std::pair<const_iterator, const_iterator> find(linetype a, subsorttype subsort1,
            subsorttype subsort2)
        {
            AddrRange addrrange(point, sub1);
            typename std::multiset<AddrRange>::const_iterator iter1, iter2;

            iter1 = tree.lower_bound(addrrange);
            if ((iter1 == tree.end()) || (point < (*iter1).first))
                return std::pair<PartIterator, PartIterator>(PartIterator(iter1), PartIterator(iter1));

            AddrRange addrend((* iter1).last, sub2);
            iter2 = tree.upper_bound(addrend);

            return std::pair<PartIterator, PartIterator>(PartIterator(iter1), PartIterator(iter2));
        }

        /// \brief Find beginning of sub-ranges that contain the given boundary point
        /// \param point is the given boundary point
        /// \return iterator to first sub-range of intersects the boundary point
        public const_iterator find_begin(linetype point)
        {
            AddrRange addrrange(point);
            typename std::multiset<AddrRange>::const_iterator iter;

            iter = tree.lower_bound(addrrange);
            return iter;
        }

        /// \brief Find ending of sub-ranges that contain the given boundary point
        /// \param point is the given boundary point
        /// \return iterator to first sub-range after that does not intersect the boundary point
        public const_iterator find_end(linetype point)
        {
            AddrRange addrend(point, subsorttype(true));
            typename std::multiset<AddrRange>::const_iterator iter;

            iter = tree.upper_bound(addrend);
            if ((iter == tree.end()) || (point < (*iter).first))
                return iter;

            // If we reach here, (*iter).last is bigger than point (as per upper_bound) but
            // point >= than (*iter).first, i.e. point is contained in the sub-range.
            // So we have to do one more search for first sub-range after the containing sub-range.
            AddrRange addrbeyond((* iter).last, subsorttype(true));
            return tree.upper_bound(addrbeyond);
        }

        /// \brief Find first record overlapping given interval
        /// \param point is the start of interval to test
        /// \param end is the end of the interval to test
        /// \return iterator to first sub-range of an intersecting record (or \b end)
        public const_iterator find_overlap(linetype point, linetype end)
        {
            AddrRange addrrange(point);
            typename std::multiset<AddrRange>::const_iterator iter;

            // First range where right boundary is equal to or past point
            iter = tree.lower_bound(addrrange);
            if (iter == tree.end()) return iter;
            if ((*iter).first <= end)
                return iter;
            return tree.end();
        }

        /// \brief Insert a new record into the container
        /// \param data is other initialization data for the new record
        /// \param a is the start of the range occupied by the new record
        /// \param b is the (inclusive) end of the range
        /// \return an iterator to the new record
        public typename std::list<_recordtype>::iterator insert(inittype data, linetype a, linetype b)
        {
            linetype f = a;
            typename std::list<_recordtype>::iterator liter;
            typename std::multiset<AddrRange>::iterator low = tree.lower_bound(AddrRange(f));

            if (low != tree.end())
            {
                if ((*low).first < f)   // Check if left boundary refines existing partition
                    unzip(f - 1, low);      // If so do the refinement
            }

            record.emplace_front(data, a, b);
            liter = record.begin();

            AddrRange addrrange(b, (* liter).getSubsort());
            addrrange.a = a;
            addrrange.b = b;
            addrrange.value = liter;
            typename std::multiset<AddrRange>::iterator spot = tree.lower_bound(addrrange);
            // Where does the new record go in full list, insert it
            record.splice((spot == tree.end()) ? record.end() : (*spot).value,
                   record, liter);

            while ((low != tree.end()) && ((*low).first <= b))
            {
                if (f <= (*low).last)
                {   // Do we overlap at all
                    if (f < (*low).first)
                    {
                        // Assume the hint makes this insert an O(1) op
                        addrrange.first = f;
                        addrrange.last = (*low).first - 1;
                        tree.insert(low, addrrange);
                        f = (*low).first;
                    }
                    if ((*low).last <= b)
                    {   // Insert as much of interval as we can
                        addrrange.first = f;
                        addrrange.last = (*low).last;
                        tree.insert(low, addrrange);
                        if ((*low).last == b) break; // Did we manage to insert it all
                        f = (*low).last + 1;
                    }
                    else if (b < (*low).last)
                    { // We can insert everything left, but must refine
                        unzip(b, low);
                        break;
                    }
                }
                ++low;
            }
            if (f <= b)
            {
                addrrange.first = f;
                addrrange.last = b;
                tree.insert(addrrange);
            }

            return liter;
        }

        /// \brief Erase a given record from the container
        /// \param v is the iterator to the record to be erased
        public void erase(typename std::list<_recordtype>::iterator v)
        {
            linetype a = (*v).getFirst();
            linetype b = (*v).getLast();
            bool leftsew = true;
            bool rightsew = true;
            bool rightoverlap = false;
            bool leftoverlap = false;
            typename std::multiset<AddrRange>::iterator low = tree.lower_bound(AddrRange(a));
            typename std::multiset<AddrRange>::iterator uplow = low;

            linetype aminus1 = a - 1;
            while (uplow != tree.begin())
            {
                --uplow;
                if ((*uplow).last != aminus1) break;
                if ((*uplow).b == aminus1)
                {
                    leftsew = false;        // Still a split between a-1 and a
                    break;
                }
            }
            do
            {
                if ((*low).value == v)
                    tree.erase(low++);
                else
                {
                    if ((*low).a < a)
                        leftoverlap = true; // a splits somebody else
                    else if ((*low).a == a)
                        leftsew = false;    // Somebody else splits at a (in addition to v)
                    if (b < (*low).b)
                        rightoverlap = true;    // b splits somebody else
                    else if ((*low).b == b)
                        rightsew = false;   // Somebody else splits at b (in addition to v)
                    low++;
                }
            } while ((low != tree.end()) && ((*low).first <= b));
            if (low != tree.end())
            {
                if ((*low).a - 1 == b)
                    rightsew = false;
            }
            if (leftsew && leftoverlap)
                zip(a - 1, tree.lower_bound(AddrRange(a - 1)));
            if (rightsew && rightoverlap)
                zip(b, tree.lower_bound(AddrRange(b)));
            record.erase(v);
        }

        /// \brief Erase a record given an iterator
        public void erase(const_iterator iter)
        {
            erase(iter.getValueIter());
        }
    }
}
