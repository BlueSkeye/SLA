using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class JumptableNotReachableError : LowlevelError
    {
        internal JumptableNotReachableError(string s)
            : base(s)
        {
        }
    }
}
