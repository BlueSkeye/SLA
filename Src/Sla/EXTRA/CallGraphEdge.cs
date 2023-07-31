using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class CallGraphEdge
    {
        public enum Flags
        {
            cycle = 1,          // Edge that was snipped to eliminate cycles
            dontfollow = 2      // Edge that is not in the spanning tree
        }
        // friend class CallGraphNode;
        // friend class CallGraph;
        internal CallGraphNode from;        // Node of the caller
        internal CallGraphNode to;      // Node of the callee
        internal Address callsiteaddr;       // Address where call was made from
        internal int complement;        // Index of complementary edge
        internal /*mutable*/ Flags flags;

        public CallGraphEdge()
        {
            flags = 0;
        }

        public bool isCycle() => ((flags & Flags.cycle) != 0);

        public void encode(Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_EDGE);
            from.getAddr().encode(encoder);
            to.getAddr().encode(encoder);
            callsiteaddr.encode(encoder);
            encoder.closeElement(ElementId.ELEM_EDGE);
        }

        public Address getCallSiteAddr() => callsiteaddr;

        public static void decode(Decoder decoder, CallGraph graph)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_EDGE);

            Address fromaddr = Address.decode(decoder);
            Address toaddr = Address.decode(decoder);
            Address siteaddr = Address.decode(decoder);
            decoder.closeElement(elemId);

            CallGraphNode fromnode = graph.findNode(fromaddr);
            if (fromnode == (CallGraphNode)null)
                throw new LowlevelError("Could not find from node");
            CallGraphNode tonode = graph.findNode(toaddr);
            if (tonode == (CallGraphNode)null)
                throw new LowlevelError("Could not find to node");

            graph.addEdge(fromnode, tonode, siteaddr);
        }
    }
}
