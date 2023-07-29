using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal struct Enumerator
    {
        internal string enumconstant;        // Identifier associated with constant
        internal bool constantassigned;  // True if user specified explicit constant
        internal ulong value;            // The actual constant

        internal Enumerator(string nm)
        {
            constantassigned = false;
            enumconstant = nm;
        }

        internal Enumerator(string nm, ulong val)
        {
            constantassigned = true;
            enumconstant = nm;
            value = val;
        }
}
}
