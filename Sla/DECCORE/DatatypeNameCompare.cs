using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// Compare two Datatype pointers: first by name, then by id
    internal struct DatatypeNameCompare
    {
        /// Comparison operator
        internal static bool operator()(Datatype a, Datatype b)
        {
            int4 res = a->getName().compare(b->getName());
            if (res != 0) return (res< 0);
            return a->getId() < b->getId();
        }
    }
}
