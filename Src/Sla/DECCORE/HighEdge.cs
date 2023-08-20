using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A record for caching a Cover intersection test between two HighVariable objects
    ///
    /// This is just a pair of HighVariable objects that can be used as a map key. The HighIntersectTest
    /// class uses it to cache intersection test results between the two variables in a map.
    internal class HighEdge
    {
        // friend class HighIntersectTest;
        /// First HighVariable of the pair
        internal HighVariable a;
        /// Second HighVariable of the pair
        internal HighVariable b;

        /// \brief Comparator
        public static bool operator <(HighEdge op1, HighEdge op2)
        {
            if (op1.a == op2.a) return (op1.b < op2.b); return (op1.a < op2.a);
        }
    
        public HighEdge(HighVariable c, HighVariable d)
        {
            a = c;
            b = d;
        }
    }
}
