using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// Compare two Datatype pointers for equivalence of their description
    internal class DatatypeCompare : IComparer<Datatype>
    {
        /// Comparison operator
        public int Compare(Datatype? a, Datatype? b)
        {
            if (null == a) throw new BugException();
            if (null == b) throw new BugException();
            int res = a.compareDependency(b);
            if (res != 0) return res;
            return a.getId().CompareTo(b.getId());
        }
    }
}
