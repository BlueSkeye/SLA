using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Comparator for sorting Symbol objects by name
    internal class SymbolCompareName : IComparer<Symbol>
    {
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
            if (comp < 0) return comp;
            if (comp > 0) return comp;
            return x.nameDedup.CompareTo(y.nameDedup);
        }
    }
}
