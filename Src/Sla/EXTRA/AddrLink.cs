using Sla.CORE;

namespace Sla.EXTRA
{
    internal struct AddrLink : IComparable<AddrLink>
    {
        internal Address a;
        internal Address b;

        internal AddrLink(Address i)
        {
            a = i;
            b = new Address();
        }

        internal AddrLink(Address i, Address j)
        {
            a = i;
            b = j;
        }

        public int CompareTo(AddrLink other)
        {
            if (this <  other) return -1;
            if (this > other) return 1;
            return 0;
        }

        public static bool operator <(AddrLink op1, AddrLink op2)
        {
            if (op1.a != op2.a) return (op1.a < op2.a);
            return (op1.b < op2.b);
        }

        public static bool operator >(AddrLink op1, AddrLink op2)
        {
            if (op1.a != op2.a) return (op1.a > op2.a);
            return (op1.b > op2.b);
        }
    }
}
