using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal struct IdentRec
    {
        internal readonly string nm;
        internal int4 id;

        internal IdentRec(string name, int4 identifier)
        {
            nm = name;
            id = identifier;
        }
    }
}
