
using System.Collections;

namespace Sla.DECCORE
{
    /// \brief An iterator over SymbolEntry objects in multiple address spaces
    /// Given an EntryMap (a rangemap of SymbolEntry objects in a single address space)
    /// for each address space, iterator over all the SymbolEntry objects
    internal class MapIterator : IEnumerator<SymbolEntry>
    {
        private bool _completed;
        private bool _disposed;
        // The list of EntryMaps, one per address space
        private List<EntryMap>? map;
        // Current EntryMap being iterated
        private IEnumerator<EntryMap> curmap;
        // Current SymbolEntry being iterated
        private IEnumerator<SymbolEntry> curiter;

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

        // Copy constructor
        internal MapIterator(MapIterator op2)
        {
            map = op2.map;
            curmap = op2.curmap;
            curiter = op2.curiter;
        }

        /// Return the SymbolEntry being pointed at
        public SymbolEntry Current
        {
            get
            {
                AssertNotDisposed();
                return curiter.Current;
            }
        }

        object IEnumerator.Current => this.Current;

        private void AssertNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        /// Pre-increment the iterator
        /// The iterator is advanced by one
        /// \return a reference to the (advanced) iterator
        public bool MoveNext()
        {
            if (curiter.MoveNext()) return true;
            while (curmap.MoveNext()) {
                curiter = curmap.Current.begin_list();
                if (curiter.MoveNext()) return true;
            }
            _completed = true;
            return false;
        }

        public void Reset()
        {
            AssertNotDisposed();
            throw new NotImplementedException();
        }

        ///// Post-increment the iterator
        ///// The iterator is advanced by one
        ///// \param i is a dummy variable
        ///// \return a copy of the iterator before it was advanced
        //internal static MapIterator operator ++(int i)
        //{
        //    MapIterator tmp = new MapIterator(this);
        //    ++curiter;
        //    while ((curmap != map.end()) && (curiter == (*curmap).end_list())) {
        //        do {
        //            ++curmap;
        //        } while ((curmap != map.end()) && ((*curmap) == (EntryMap)null));
        //        if (curmap != map.end())
        //            curiter = (*curmap).begin_list();
        //    }
        //    return tmp;
        //}

        //// Assignment operator
        //internal MapIterator operator=(MapIterator op2)
        //{
        //    map = op2.map;
        //    curmap = op2.curmap;
        //    curiter = op2.curiter;
        //    return this;
        //}

        // Equality operator
        public static bool operator ==(MapIterator op1, MapIterator op2)
        {
            if (op1.curmap != op2.curmap) return false;
            if (op1._completed) return true;
            return (op1.curiter.Current == op2.curiter.Current);
        }

        // Inequality operator
        public static bool operator !=(MapIterator op1, MapIterator op2)
        {
            return !(op1 == op2);
        }
    }
}
