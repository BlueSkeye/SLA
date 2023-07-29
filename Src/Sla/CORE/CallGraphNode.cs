using Sla.CORE;
using Sla.DECCORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    internal class CallGraphNode
    {
        [Flags()]
        public enum Flags
        {
            mark = 1,
            onlycyclein = 2,
            currentcycle = 4,
            entrynode = 8
        }
        // friend class CallGraph;
        private Address entryaddr;      // Starting address of function
        private string name;            // Name of the function if available
        private Funcdata fd;           // Pointer to funcdata if we have it
        private List<CallGraphEdge> inedge;
        private List<CallGraphEdge> outedge;
        private int parentedge;        // Incoming edge for spanning tree
        private /*mutable*/ Flags flags;

        public CallGraphNode()
        {
            fd = (Funcdata*)0;
            flags = 0;
            parentedge = -1;
        }
        
        public void clearMark() 
        {
            flags &= ~((uint) mark);
        }

        public bool isMark() => ((flags&mark)!=0);

        public Address getAddr() => entryaddr;

        public string getName() => name;

        public Funcdata getFuncdata() => fd;

        public int numInEdge() => inedge.size();

        public CallGraphEdge getInEdge(int i) => inedge[i];

        public CallGraphNode getInNode(int i) => inedge[i].from;

        public int numOutEdge() => outedge.size();

        public CallGraphEdge getOutEdge(int i) => outedge[i];

        public CallGraphNode getOutNode(int i) => outedge[i].to;

        public void setFuncdata(Funcdata f)
        {
            if ((fd != (Funcdata*)0) && (fd != f))
                throw new LowlevelError("Multiple functions at one address in callgraph");

            if (f.getAddress() != entryaddr)
                throw new LowlevelError("Setting function data at wrong address in callgraph");
            fd = f;
        }

        public void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_NODE);
            if (name.size() != 0)
                encoder.writeString(ATTRIB_NAME, name);
            entryaddr.encode(encoder);
            encoder.closeElement(ELEM_NODE);
        }

        public static void decode(Decoder decoder, CallGraph graph)
        {
            uint elemId = decoder.openElement(ELEM_NODE);
            string name;
            for (; ; )
            {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_NAME)
                    name = decoder.readString();
            }
            Address addr = Address::decode(decoder);
            decoder.closeElement(elemId);
            graph.addNode(addr, name);
        }
    }
}
