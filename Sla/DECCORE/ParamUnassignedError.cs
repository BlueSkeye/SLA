using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Exception thrown when a prototype can't be modeled properly
    internal class ParamUnassignedError : LowlevelError
    {
        internal ParamUnassignedError(string s)
            : base(s)
        {
        }
    }
}
