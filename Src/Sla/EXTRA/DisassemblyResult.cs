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
        internal int length;
        internal uint flags;
        internal Address jumpaddress;
        internal ulong targethit;
    }
}
