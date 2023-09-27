using Sla.CORE;
using System.Collections;

namespace Sla.DECCORE
{
    /// \brief Map object for keeping track of which address ranges have been heritaged
    ///
    /// We keep track of a fairly fine grained description of when each address range
    /// was entered in SSA form, referred to as \b heritaged or, for Varnode objects,
    /// no longer \b free.  An address range is added using the add() method, which includes
    /// the particular pass when it was entered.  The map can be queried using findPass()
    /// that informs the caller whether the address has been heritaged and if so in which pass.
    internal class LocationMap
    {
        /// Iterator into the main map
        // typedef Dictionary<Address, SizePass>::iterator iterator;
        /// Heritaged addresses mapped to range size and pass number
        private Dictionary<Address, SizePass> themap = new Dictionary<Address, SizePass>();

        /// Mark new address as \b heritaged
        /// Update disjoint cover making sure (addr,size) is contained in a single element and return
        /// an iterator to this element. The element's \b pass number is set to be the smallest value
        /// of any previous intersecting element. Additionally an \b intersect code is passed back:
        ///   - 0 if the only intersection is with range from the same pass
        ///   - 1 if there is a partial intersection with something old
        ///   - 2 if the range is contained in an old range
        /// \param addr is the starting address of the range to add
        /// \param size is the number of bytes in the range
        /// \param pass is the pass number when the range was heritaged
        /// \param intersect is a reference for passing back the intersect code
        /// \return the iterator to the map element containing the added range
        public SizePass add(Address addr, int size, int pass, out int intersect)
        {
            Dictionary<Address, SizePass>.Enumerator iter = themap.lower_bound(addr);
            if (iter != themap.begin())
                --iter;
            if (   (iter != themap.end())
                && (-1 == addr.overlap(0, iter.Current.Key, iter.Current.Value.size)))
            {
                ++iter;
            }

            int where = 0;
            intersect = 0;
            if (   (iter != themap.end())
                && (-1 != (where = addr.overlap(0, iter.Current.Key, iter.Current.Value.size))))
            {
                if (where + size <= iter.Current.Value.size) {
                    // Completely contained in previous element
                    intersect = (iter.Current.Value.pass < pass) ? 2 : 0;
                    return iter.Current;
                }
                addr = iter.Current.Key;
                size = where + size;
                if (iter.Current.Value.pass < pass) {
                    // Partial overlap with old element
                    intersect = 1;
                    pass = iter.Current.Value.pass;
                }
                themap.erase(iter++);
            }
            while (   (iter != themap.end())
                   && (-1 != (where = iter.Current.Key.overlap(0, addr, size))))
            {
                if (where + iter.Current.Value.size > size) {
                    size = where + iter.Current.Value.size;
                }
                if (iter.Current.Value.pass < pass) {
                    intersect = 1;
                    pass = iter.Current.Value.pass;
                }
                themap.erase(iter++);
            }
            SizePass newPass = new SizePass() {
                size = size,
                pass = pass
            };
            themap.Add(addr, newPass);
            return iter;
        }

        /// Look up if/how given address was heritaged
        /// If the given address was heritaged, return (the iterator to) the SizeMap entry
        /// describing the associated range and when it was heritaged.
        /// \param addr is the given address
        /// \return the iterator to the SizeMap entry or the end iterator is the address is unheritaged
        public IEnumerator find(Address addr)
        {
            // First range after address
            Dictionary<Address, SizePass>.Enumerator iter = themap.upper_bound(addr);
            if (iter == themap.begin()) {
                return themap.end();
            }
            // First range before or equal to address
            --iter;
            return (-1 != addr.overlap(0, iter.Current.Key, iter.Current.Value.size))
                ? iter
                : themap.end();
        }

        /// Look up if/how given address was heritaged
        /// Return the pass number when the given address was heritaged, or -1 if it was not heritaged
        /// \param addr is the given address
        /// \return the pass number of -1
        public int findPass(Address addr)
        {
            // First range after address
            IEnumerator<KeyValuePair<Address, SizePass>> iter = themap.upper_bound(addr);
            if (iter == themap.begin()) return -1;
            // First range before or equal to address
            --iter;
            return (-1 != addr.overlap(0, iter.Current.Key, iter.Current.Value.size))
                ? iter.Current.Value.pass
                : -1;
        }

        /// Remove a particular entry from the map
        public void erase(Address removed)
        {
            themap.Remove(removed);
        }

        // Get starting iterator over heritaged ranges
        public Dictionary<Address, SizePass>.Enumerator begin() => themap.GetEnumerator();

        //// Get ending iterator over heritaged ranges
        //public IEnumerator end() => themap.end();

        /// Clear the map of heritaged ranges
        public void clear()
        {
            themap.Clear();
        }
    }
}
