using System.Collections.Generic;

namespace Sla.DECCORE
{
    // A set of Varnodes sorted by location (then by definition)
    // VarnodeCompareLocDef
    internal class VarnodeLocSet : SortedSet<Varnode>
    {
        internal VarnodeLocSet()
            : base(VarnodeCompareLocDef.Instance)
        {
        }
    }
}
