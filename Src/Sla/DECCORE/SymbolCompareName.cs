using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Comparator for sorting Symbol objects by name
    internal class SymbolCompareName
    {
        /// \brief Compare two Symbol pointers
        /// Compare based on name. Use the deduplication id on the symbols if necessary
        /// \param sym1 is the first Symbol
        /// \param sym2 is the second Symbol
        /// \return \b true if the first is ordered before the second
        public bool operator()(Symbol sym1, Symbol sym2)
        {
            int comp = sym1.name.compare(sym2.name);
            if (comp< 0) return true;
            if (comp > 0) return false;
            return (sym1.nameDedup<sym2.nameDedup);
        }
    }
}
