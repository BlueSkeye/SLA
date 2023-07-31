using Sla.DECCORE;
using Sla.EXTRA;

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
        internal Address entryaddr;      // Starting address of function
        internal string? name;            // Name of the function if available
        internal Funcdata? fd;           // Pointer to funcdata if we have it
        internal List<CallGraphEdge> inedge;
        internal List<CallGraphEdge> outedge;
        internal int parentedge;        // Incoming edge for spanning tree
        internal /*mutable*/ Flags flags;

        public CallGraphNode()
        {
            fd = (Funcdata)null;
            flags = 0;
            parentedge = -1;
        }
        
        public void clearMark() 
        {
            flags &= ~Flags.mark;
        }

        public bool isMark() => ((flags & Flags.mark)!=0);

        public Address getAddr() => entryaddr;

        public string getName() => name;

        public Funcdata? getFuncdata() => fd;

        public int numInEdge() => inedge.Count;

        public CallGraphEdge getInEdge(int i) => inedge[i];

        public CallGraphNode getInNode(int i) => inedge[i].from;

        public int numOutEdge() => outedge.Count;

        public CallGraphEdge getOutEdge(int i) => outedge[i];

        public CallGraphNode getOutNode(int i) => outedge[i].to;

        public void setFuncdata(Funcdata f)
        {
            if ((fd != (Funcdata)null) && (object.ReferenceEquals(fd, f)))
                throw new LowlevelError("Multiple functions at one address in callgraph");

            if (f.getAddress() != entryaddr)
                throw new LowlevelError("Setting function data at wrong address in callgraph");
            fd = f;
        }

        public void encode(Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_NODE);
            if (name.Length != 0)
                encoder.writeString(AttributeId.ATTRIB_NAME, name);
            entryaddr.encode(encoder);
            encoder.closeElement(ElementId.ELEM_NODE);
        }

        public static void decode(Decoder decoder, CallGraph graph)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_NODE);
            string? name = null;
            for (; ; ) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_NAME)
                    name = decoder.readString();
            }
            Address addr = Address.decode(decoder);
            decoder.closeElement(elemId);
            graph.addNode(addr, name);
        }
    }
}
