using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal struct ConstructState
    {
        internal Constructor ct;
        internal FixedHandle hand;
        internal List<ConstructState> resolve;
        internal ConstructState parent;
        internal int length;            // Length of this instantiation of the constructor
        internal uint offset;			// Absolute offset (from start of instruction)
    }
}
