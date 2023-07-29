using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal struct DisassemblyResult
    {
        internal bool success;
        internal int4 length;
        internal uint4 flags;
        internal Address jumpaddress;
        internal uintb targethit;
    }
}
