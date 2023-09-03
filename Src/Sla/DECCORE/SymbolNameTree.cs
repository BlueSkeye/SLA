
namespace Sla.DECCORE
{
    internal class SymbolNameTree : SortedSet<Symbol>
    {
        internal SymbolNameTree()
            : base(_Comparer.Singleton)
        {
        }

        internal bool empty() => (0 == base.Count);

        private class _Comparer : IComparer<Symbol>
        {
            internal static readonly _Comparer Singleton = new _Comparer();

            private _Comparer() { }

            /// \brief Compare two Symbol pointers
            /// Compare based on name. Use the deduplication id on the symbols if necessary
            /// \param sym1 is the first Symbol
            /// \param sym2 is the second Symbol
            /// \return \b true if the first is ordered before the second
            public int Compare(Symbol? x, Symbol? y)
            {
                if (null == x) throw new BugException();
                if (null == y) throw new BugException();
                int comp = x.name.CompareTo(y.name);
                return (0 == comp) ? x.nameDedup.CompareTo(y.nameDedup) : comp;
            }
        }
    }
}
