using Sla.SLACOMP;
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
        internal sleightokentype id;

        internal IdentRec(string name, sleightokentype identifier)
        {
            nm = name;
            id = identifier;
        }
    }
}
