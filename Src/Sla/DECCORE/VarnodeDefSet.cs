using System.Collections.Generic;

namespace Sla.DECCORE
{
    // A set of Varnodes sorted by definition (then location)
    internal class VarnodeDefSet : SortedSet<Varnode>
    {
        internal VarnodeDefSet()
            : base(VarnodeCompareDefLoc.Instance)
        {
        }
    }
}
