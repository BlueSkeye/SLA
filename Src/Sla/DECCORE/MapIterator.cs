using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An iterator over SymbolEntry objects in multiple address spaces
    /// Given an EntryMap (a rangemap of SymbolEntry objects in a single address space)
    /// for each address space, iterator over all the SymbolEntry objects
    internal class MapIterator
    {
        private List<EntryMap>? map;       ///< The list of EntryMaps, one per address space
        private IEnumerator<EntryMap> curmap;   ///< Current EntryMap being iterated
        private IEnumerator<SymbolEntry> curiter;  ///< Current SymbolEntry being iterated

        /// Construct an uninitialized iterator
        public MapIterator()
        {
            map = null;
        }

        /// \brief Construct iterator at a specific position
        /// \param m is the list of EntryMaps
        /// \param cm is the position of the iterator within the EntryMap list
        /// \param ci is the position of the iterator within the specific EntryMap
        internal MapIterator(List<EntryMap> m, IEnumerator<EntryMap> cm,
            IEnumerator<SymbolEntry> ci)
        {
            map = m;
            curmap = cm;
            curiter = ci;
        }

        /// \brief Copy constructor
        internal MapIterator(MapIterator op2)
        {
            map = op2.map;
            curmap = op2.curmap;
            curiter = op2.curiter;
        }

        /// Return the SymbolEntry being pointed at
        internal SymbolEntry operator*()
        {
            return &(* curiter);
        }

        /// Pre-increment the iterator
        /// The iterator is advanced by one
        /// \return a reference to the (advanced) iterator
        internal static MapIterator operator ++()
        {
            ++curiter;
            while ((curmap != map.end()) && (curiter == (*curmap).end_list()))
            {
                do
                {
                    ++curmap;
                } while ((curmap != map.end()) && ((*curmap) == (EntryMap)null));
                if (curmap != map.end())
                    curiter = (*curmap).begin_list();
            }
            return *this;
        }

        /// Post-increment the iterator
        /// The iterator is advanced by one
        /// \param i is a dummy variable
        /// \return a copy of the iterator before it was advanced
        internal static MapIterator operator ++(int i)
        {
            MapIterator tmp = new MapIterator(*this);
            ++curiter;
            while ((curmap != map.end()) && (curiter == (*curmap).end_list()))
            {
                do
                {
                    ++curmap;
                } while ((curmap != map.end()) && ((*curmap) == (EntryMap)null));
                if (curmap != map.end())
                    curiter = (*curmap).begin_list();
            }
            return tmp;
        }

        /// \brief Assignment operator
        internal MapIterator operator=(MapIterator op2)
        {
            map = op2.map;
            curmap = op2.curmap;
            curiter = op2.curiter;
            return *this;
        }

        /// \brief Equality operator
        internal bool operator ==(MapIterator op2)
        {
            if (curmap != op2.curmap) return false;
            if (curmap == map.end()) return true;
            return (curiter == op2.curiter);
        }

        /// \brief Inequality operator
        internal bool operator !=(MapIterator op2)
        {
            if (curmap != op2.curmap) return true;
            if (curmap == map.end()) return false;
            return (curiter != op2.curiter);
        }
    }
}
