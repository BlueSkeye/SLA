
namespace Sla.DECCORE
{
    // \brief A record for caching a Cover intersection test between two HighVariable objects
    // This is just a pair of HighVariable objects that can be used as a map key. The HighIntersectTest
    // class uses it to cache intersection test results between the two variables in a map.
    internal class HighEdge : IComparable<HighEdge>
    {
        // friend class HighIntersectTest;
        // First HighVariable of the pair
        internal HighVariable a;
        // Second HighVariable of the pair
        internal HighVariable b;

        /// \brief Comparator
        public static bool operator <(HighEdge op1, HighEdge op2)
            => (op1.a == op2.a) ? (0 > op1.b.CompareTo(op2.b)) : (0 > op1.a.CompareTo(op2.a));

        public static bool operator >(HighEdge op1, HighEdge op2)
            => (op1.a == op2.a) ? (0 < op1.b.CompareTo(op2.b)) : (0 < op1.a.CompareTo(op2.a));

        public HighEdge(HighVariable c, HighVariable d)
        {
            a = c;
            b = d;
        }

        public int CompareTo(HighEdge? other)
        {
            if (null == other) throw new ArgumentNullException();
            if (this < other) return -1;
            if (this > other) return 1;
            return 0;
        }
    }
}
