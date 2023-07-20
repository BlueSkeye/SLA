using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
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
        // typedef map<Address, SizePass>::iterator iterator;
        /// Heritaged addresses mapped to range size and pass number
        private Dictionary<Address, SizePass> themap;

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
        public IEnumerator add(Address addr, int4 size, int4 pass, int4 intersect)
        {
            iterator iter = themap.lower_bound(addr);
            if (iter != themap.begin())
                --iter;
            if ((iter != themap.end()) && (-1 == addr.overlap(0, (*iter).first, (*iter).second.size)))
                ++iter;

            int4 where = 0;
            intersect = 0;
            if ((iter != themap.end()) && (-1 != (where = addr.overlap(0, (*iter).first, (*iter).second.size))))
            {
                if (where + size <= (*iter).second.size)
                {
                    intersect = ((*iter).second.pass < pass) ? 2 : 0; // Completely contained in previous element
                    return iter;
                }
                addr = (*iter).first;
                size = where + size;
                if ((*iter).second.pass < pass)
                {
                    intersect = 1;          // Partial overlap with old element
                    pass = (*iter).second.pass;
                }
                themap.erase(iter++);
            }
            while ((iter != themap.end()) && (-1 != (where = (*iter).first.overlap(0, addr, size))))
            {
                if (where + (*iter).second.size > size)
                    size = where + (*iter).second.size;
                if ((*iter).second.pass < pass)
                {
                    intersect = 1;
                    pass = (*iter).second.pass;
                }
                themap.erase(iter++);
            }
            iter = themap.insert(pair<Address, SizePass>(addr, SizePass())).first;
            (*iter).second.size = size;
            (*iter).second.pass = pass;
            return iter;
        }

        /// Look up if/how given address was heritaged
        /// If the given address was heritaged, return (the iterator to) the SizeMap entry
        /// describing the associated range and when it was heritaged.
        /// \param addr is the given address
        /// \return the iterator to the SizeMap entry or the end iterator is the address is unheritaged
        public iterator find(Address addr)
        {
            iterator iter = themap.upper_bound(addr); // First range after address
            if (iter == themap.begin()) return themap.end();
            --iter;         // First range before or equal to address
            if (-1 != addr.overlap(0, (*iter).first, (*iter).second.size))
                return iter;
            return themap.end();
        }

        /// Look up if/how given address was heritaged
        /// Return the pass number when the given address was heritaged, or -1 if it was not heritaged
        /// \param addr is the given address
        /// \return the pass number of -1
        public int4 findPass(Address addr)
        {
            map<Address, SizePass>::const_iterator iter = themap.upper_bound(addr); // First range after address
            if (iter == themap.begin()) return -1;
            --iter;         // First range before or equal to address
            if (-1 != addr.overlap(0, (*iter).first, (*iter).second.size))
                return (*iter).second.pass;
            return -1;
        }

        /// Remove a particular entry from the map
        public void erase(iterator iter)
        {
            themap.erase(iter);
        }

        /// Get starting iterator over heritaged ranges
        public IEnumerator begin() => themap.begin();

        /// Get ending iterator over heritaged ranges
        public IEnumerator end() => themap.end();

        /// Clear the map of heritaged ranges
        public void clear()
        {
            themap.clear();
        }
    }
}
