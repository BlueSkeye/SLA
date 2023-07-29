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
        internal int id;

        internal IdentRec(string name, int identifier)
        {
            nm = name;
            id = identifier;
        }
    }
}
