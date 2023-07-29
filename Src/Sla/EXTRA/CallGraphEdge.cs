using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private CallGraphNode from;        // Node of the caller
        private CallGraphNode to;      // Node of the callee
        private Address callsiteaddr;       // Address where call was made from
        private int4 complement;        // Index of complementary edge
        private /*mutable*/ Flags flags;

        public CallGraphEdge()
        {
            flags = 0;
        }

        public bool isCycle() => ((flags&1)!=0);

        public void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_EDGE);
            from.getAddr().encode(encoder);
            to.getAddr().encode(encoder);
            callsiteaddr.encode(encoder);
            encoder.closeElement(ELEM_EDGE);
        }

        public Address getCallSiteAddr() => callsiteaddr;

        public static void decode(Decoder decoder, CallGraph graph)
        {
            uint4 elemId = decoder.openElement(ELEM_EDGE);
            Address fromaddr, toaddr, siteaddr;

            fromaddr = Address::decode(decoder);
            toaddr = Address::decode(decoder);
            siteaddr = Address::decode(decoder);
            decoder.closeElement(elemId);

            CallGraphNode* fromnode = graph.findNode(fromaddr);
            if (fromnode == (CallGraphNode*)0)
                throw new LowlevelError("Could not find from node");
            CallGraphNode* tonode = graph.findNode(toaddr);
            if (tonode == (CallGraphNode*)0)
                throw new LowlevelError("Could not find to node");

            graph.addEdge(fromnode, tonode, siteaddr);
        }
    }
}
