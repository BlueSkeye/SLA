using ghidra;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.EXTRA
{
    /// \brief A map from a linear space to value objects
    /// The partmap is a template class taking:
    ///   -  _linetype which represents an element in the linear space
    ///   -  _valuetype which are the objects that linear space maps to
    /// Let R be the linear space with an ordering, and let { a_i } be a finite set
    /// of points in R.
    /// The a_i partition R into a finite number of disjoint sets
    /// { x : x < a_0 },  { x : x>=a_0 && x < a_1 }, ...
    ///                   { x : x>=a_i && x < a_i+1 }, ...
    ///                   { x : x>=a_n }
    /// A partmap maps elements of this partition to _valuetype objects
    /// A _valuetype is then associated with any element x in R by
    /// looking up the value associated with the partition element containing x.
    /// The map is defined by starting with a \e default value object that applies
    /// to the whole linear space.  Then \e split points are introduced, one at a time,
    /// in the linear space. At each split point, the associated value object is split
    /// into two objects.  At any point the value object describing some part of the linear space
    /// can be changed.
    internal class partmap<_linetype, _valuetype>
        where _linetype : notnull, IComparable<_linetype>
    {
        //public:
        ///// Defining the map from split points to value objects
        //typedef std::map<_linetype, _valuetype> maptype;
        ///// A partmap iterator is an iterator into the map
        //typedef typename maptype::iterator iterator;
        ///// A constant iterator
        //typedef typename maptype::const_iterator const_iterator;

        /// Map from linear split points to the value objects
        private SortedDictionary<_linetype, _valuetype> database;
        /// The value object \e before the first split point
        private _valuetype defaultvalue;
        
        /// Get the value object at a point
        /// Look up the first split point coming before the given point
        /// and return the value object it maps to. If there is no earlier split point
        /// return the default value.
        /// \param pnt is the given point in the linear space
        /// \return the corresponding value object
        public _valuetype getValue(_linetype pnt)
        {
            bool found;
            bool exactMatch;
            KeyValuePair<_linetype, _valuetype>? result =
                database.BeforeUpperBound(pnt, out found, out exactMatch);
            if (found) {
                if (!result.HasValue) {
                    throw new ApplicationException("BUG");
                }
                return result.Value.Value;
            }
            return defaultvalue;
        }

        /// Introduce a new split point
        /// Add (if not already present) a point to the linear partition.
        /// \param pnt is the (new) point
        /// \return the (possibly) new value object for the range starting at the point
        public _valuetype split(_linetype pnt)
        {
            bool found;
            bool exactMatch;
            KeyValuePair<_linetype, _valuetype>? result =
                database.BeforeUpperBound(pnt, out found, out exactMatch);

            if (found) {
                if (!result.HasValue) {
                    throw new ApplicationException("BUG");
                }
                if (exactMatch) {
                    // point matches exactly
                    // Return old ref
                    return result.Value.Value;
                }
                // Create new ref at point
                database.Add(pnt, result.Value.Value);
                return result.Value.Value;
            }
            // Create new ref at point
            database.Add(pnt, defaultvalue);
            return defaultvalue;
        }

        /// \brief Get the value object for a given point and return the range over which the value object applies
        /// Pass back a \b before and \b after point defining the maximal range over which the value applies.
        /// An additional validity code is passed back describing which of the bounding points apply:
        ///   - 0 if both bounds apply
        ///   - 1 if there is no lower bound
        ///   - 2 if there is no upper bound,
        ///   - 3 if there is neither a lower or upper bound
        /// \param pnt is the given point around which to compute the range
        /// \param before is a reference to the passed back lower bound
        /// \param after is a reference to the passed back upper bound
        /// \param valid is a reference to the passed back validity code
        /// \return the corresponding value object
        public _valuetype bounds(_linetype pnt, ref _linetype before,
            ref _linetype after, out int valid)
        {
            if (0 == database.Count) {
                valid = 3;
                return defaultvalue;
            }
            KeyValuePair<_linetype, _valuetype>? lastFound = null;
            bool onFirstEntry = true;
            foreach (KeyValuePair<_linetype, _valuetype> pair in database) {
                lastFound = pair;
                int comparison = pair.Key.CompareTo(pnt);
                if (0 >= comparison) {
                    before = pair.Key;
                    onFirstEntry = false;
                    continue;
                }
                if (onFirstEntry) {
                    // No lowerbound
                    valid = 1;
                    after = pair.Key;
                    return defaultvalue;
                }
                // Fully bounded
                valid = 0;
                return pair.Value;
            }
            if (!lastFound.HasValue) {
                throw new ApplicationException("BUG");
            }
            // No upperbound
            valid = 2;
            return lastFound.Value.Value;
        }
        
        /// Get the default value object
        public ref _valuetype defaultValue()
        {
            return ref defaultvalue;
        }

        /// Clear a range of split points
        /// Split points are introduced at the two boundary points of the given range,
        /// and all split points in between are removed. The value object that was initially
        /// present at the left-most boundary point becomes the value (as a copy) for the
        /// whole range.
        /// \param pnt1 is the left-most boundary point of the range
        /// \param pnt2 is the right-most boundary point
        /// \return the value object assigned to the range
        public _valuetype clearRange(_linetype pnt1, _linetype pnt2)
        {
            List<_linetype> removedItems = new List<_linetype>();

            split(pnt1);
            split(pnt2);
            bool firstPointFound = false;
            bool secondPointFound = false;
            _valuetype result = default(_valuetype);
            foreach (KeyValuePair<_linetype, _valuetype> pair in database) {
                if (pair.Key.Equals(pnt1)) {
                    firstPointFound = true;
                    result = pair.Value;
                }
                else if (pair.Key.Equals(pnt2)) {
                    secondPointFound = true;
                }
                if (firstPointFound) {
                    removedItems.Add(pair.Key);
                }
                if (secondPointFound) {
                    break;
                }
            }
            if (!firstPointFound) {
                throw new BugException();
            }
            foreach(_linetype key in removedItems) {
                database.Remove(key);
            }
            return result;
        }

        /// Beginning of split points
        //const_iterator begin(void) { return database.begin(); }

        /// End of split points
        //const_iterator end(void) { return database.end(); }

        /// Beginning of split points
        public IEnumerator<KeyValuePair<_linetype, _valuetype>>? begin()
        {
            return database.GetEnumerator();
        }

        /// End of split points
        //iterator end(void) { return database.end(); }

        /// Get first split point after given point
        public IEnumerator<KeyValuePair<_linetype, _valuetype>>? begin(_linetype pnt)
        {
            IEnumerator<KeyValuePair<_linetype, _valuetype>> result = database.GetEnumerator();
            while (true) {
                if (!result.MoveNext()) {
                    return null;
                }
                if (0 <= result.Current.Key.CompareTo(pnt)) {
                    break;
                }
            }
            return result;
        }

        /// Clear all split points
        public void clear()
        {
            database.Clear();
        }

        /// Return \b true if there are no split points
        public bool empty()
        {
            return 0 == database.Count;
        }
    }
}
