using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal struct AddrLink
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
