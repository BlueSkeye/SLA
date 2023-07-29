using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// Compare two Datatype pointers for equivalence of their description
    internal struct DatatypeCompare
    {
        /// Comparison operator
        internal static bool operator()(Datatype a, Datatype b)
        {
            int4 res = a->compareDependency(*b);
            if (res != 0) return (res<0);
            return a->getId() < b->getId();
        }
    }
}
