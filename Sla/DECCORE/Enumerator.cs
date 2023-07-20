using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal struct Enumerator
    {
        internal string enumconstant;        // Identifier associated with constant
        internal bool constantassigned;  // True if user specified explicit constant
        internal uintb value;            // The actual constant

        internal Enumerator(string nm)
        {
            constantassigned = false;
            enumconstant = nm;
        }

        internal Enumerator(string nm, uintb val)
        {
            constantassigned = true;
            enumconstant = nm;
            value = val;
        }
}
}
