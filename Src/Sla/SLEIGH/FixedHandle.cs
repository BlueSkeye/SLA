using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal struct FixedHandle
    {
        // A handle that is fully resolved
        internal AddrSpace space;
        internal uint size;
        internal AddrSpace offset_space;    // Either null or where dynamic offset is stored
        internal ulong offset_offset;        // Either static offset or ptr offset
        internal uint offset_size;      // Size of pointer
        internal AddrSpace temp_space;  // Consistent temporary location for value
        internal ulong temp_offset;
    }
}
