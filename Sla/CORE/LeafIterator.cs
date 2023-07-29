using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal struct LeafIterator
    {
        internal CallGraphNode node;
        internal int4 outslot;

        internal LeafIterator(CallGraphNode n)
        {
            node = n;
            outslot = 0;
        }
    }
}
