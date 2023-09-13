using Sla.DECCORE;
using Sla.EXTRA;

namespace Sla.CORE
{
    internal class CallGraph
    {
        private Architecture glb;
        // Nodes in the graph sorted by address
        private Dictionary<Address, CallGraphNode> graph;
        private List<CallGraphNode> seeds;

        private bool findNoEntry(List<CallGraphNode> seeds)
        {
            // Find all functions (that are not already marked) that either have no in edges at all,
            // or have no in edges that haven't been snipped as part of cycles

            CallGraphNode? lownode = (CallGraphNode)null;
            bool allcovered = true;
            bool newseeds = false;

            foreach (KeyValuePair<Address, CallGraphNode> pair in graph) {
                CallGraphNode node = pair.Value;
                if (node.isMark()) continue;
                if ((node.inedge.Count == 0) || ((node.flags & CallGraphNode.Flags.onlycyclein) != 0)) {
                    seeds.Add(node);
                    node.flags |= CallGraphNode.Flags.mark | CallGraphNode.Flags.entrynode;
                    newseeds = true;
                }
                else {
                    allcovered = false;
                    // We need to worry about the case where everything is in a cycle, so we don't find a natural root
                    // We use the node with the lowest number of in edges as a pseudo root
                    if (lownode == (CallGraphNode)null)
                        lownode = node;
                    else {
                        if (node.numInEdge() < lownode.numInEdge())
                            lownode = node;
                    }
                }
            }
            if (!newseeds && !allcovered) {
                seeds.Add(lownode);
                lownode.flags |= CallGraphNode.Flags.mark | CallGraphNode.Flags.entrynode;
            }
            return allcovered;
        }

        private void snipCycles(CallGraphNode node)
        {
            // Snip any cycles starting from root -node-
            CallGraphNode next;
            List<LeafIterator> stack = new List<LeafIterator>();

            node.flags |= CallGraphNode.Flags.currentcycle;
            stack.Add(new LeafIterator(node));

            while (0 != stack.Count) {
                CallGraphNode cur = stack.GetLastItem().node; // Current node
                int st = stack.GetLastItem().outslot; // which out edge we will follow
                if (st >= cur.outedge.Count) {
                    cur.flags &= ~CallGraphNode.Flags.currentcycle;
                    stack.RemoveLastItem();
                }
                else {
                    LeafIterator leafIterator = stack.GetLastItem();
                    leafIterator.outslot += 1;
                    if ((cur.outedge[st].flags & CallGraphEdge.Flags.cycle) != 0) continue;
                    next = cur.outedge[st].to;
                    if ((next.flags & CallGraphNode.Flags.currentcycle) != 0) {
                        // Found a cycle
                        snipEdge(cur, st);
                        continue;
                    }
                    else if ((next.flags & CallGraphNode.Flags.mark) != 0) {
                        // Already traced before
                        cur.outedge[st].flags |= CallGraphEdge.Flags.dontfollow;
                        continue;
                    }
                    next.parentedge = cur.outedge[st].complement;
                    next.flags |= (CallGraphNode.Flags.currentcycle | CallGraphNode.Flags.mark);
                    stack.Add(new LeafIterator(next));
                }
            }
        }

        private void snipEdge(CallGraphNode node, int i)
        {
            node.outedge[i].flags |= CallGraphEdge.Flags.cycle | CallGraphEdge.Flags.dontfollow;
            int toi = node.outedge[i].complement;
            CallGraphNode to = node.outedge[i].to;
            to.inedge[toi].flags |= CallGraphEdge.Flags.cycle;
            bool onlycycle = true;
            for (uint j = 0; j < to.inedge.Count; ++j) {
                if ((to.inedge[(int)j].flags & CallGraphEdge.Flags.cycle) == 0) {
                    onlycycle = false;
                    break;
                }
            }
            if (onlycycle)
                to.flags |= CallGraphNode.Flags.onlycyclein;
        }

        private void clearMarks()
        {
            foreach (CallGraphNode node in graph.Values)
                node.clearMark();
        }

        private void cycleStructure()
        {
            // Generate list of seeds nodes (from which we can get to everything)
            if (0 != seeds.Count)
                return;
            uint walked = 0;
            bool allcovered;

            do {
                allcovered = findNoEntry(seeds);
                while (walked < seeds.Count) {
                    CallGraphNode rootnode = seeds[(int)walked];
                    rootnode.parentedge = (int)walked;
                    snipCycles(rootnode);
                    walked += 1;
                }
            } while (!allcovered);
            clearMarks();
        }

        private CallGraphNode popPossible(CallGraphNode node, out int outslot)
        {
            if ((node.flags & CallGraphNode.Flags.entrynode) != 0) {
                outslot = node.parentedge;
                return (CallGraphNode)null;
            }
            outslot = node.inedge[node.parentedge].complement;
            return node.inedge[node.parentedge].from;
        }

        private CallGraphNode pushPossible(CallGraphNode node, int outslot)
        {
            if (node == (CallGraphNode)null) {
                if (outslot >= seeds.Count)
                    return (CallGraphNode)null;
                return seeds[outslot];
            }
            while (outslot < node.outedge.Count) {
                if ((node.outedge[outslot].flags & CallGraphEdge.Flags.dontfollow) != 0)
                    outslot += 1;
                else
                    return node.outedge[outslot].to;
            }
            return (CallGraphNode)null;
        }

        private CallGraphEdge insertBlankEdge(CallGraphNode node, int slot)
        {
            node.outedge.Add(new CallGraphEdge());
            if (node.outedge.Count > 1) {
                for (int i = node.outedge.Count - 2; i >= slot; --i) {
                    int newi = i + 1;
                    CallGraphEdge edge = node.outedge[newi];
                    edge = node.outedge[i];
                    CallGraphNode nodeout = edge.to;
                    nodeout.inedge[edge.complement].complement += 1;
                }
            }
            return node.outedge[slot];
        }

        private void iterateScopesRecursive(Scope scope)
        {
            if (!scope.isGlobal()) return;
            iterateFunctionsAddrOrder(scope);
            ScopeMap.Enumerator iter = scope.childrenBegin();
            while(iter.MoveNext()) {
                iterateScopesRecursive(iter.Current.Value);
            }
        }

        private void iterateFunctionsAddrOrder(Scope scope)
        {
            IEnumerator<SymbolEntry> miter = scope.begin();
            /// MapIterator menditer = scope.end();
            while (miter.MoveNext()) {
                Symbol? sym = miter.Current.getSymbol();
                FunctionSymbol? fsym = (FunctionSymbol)(sym);
                if (fsym != (FunctionSymbol)null)
                    addNode(fsym.getFunction());
            }
        }

        public CallGraph(Architecture g)
        {
            glb = g;
        }

        public CallGraphNode addNode(Funcdata f)
        {
            // Add a node, based on an existing function -f-
            CallGraphNode node = graph[f.getAddress()];

            if ((node.getFuncdata() != (Funcdata)null) && (node.getFuncdata() != f))
                throw new LowlevelError(
                    $"Functions with duplicate entry points: {f.getName()} {node.getFuncdata().getName()}");

            node.entryaddr = f.getAddress();
            node.name = f.getDisplayName();
            node.fd = f;
            return node;
        }

        public CallGraphNode addNode(Address addr, string? nm)
        {
            CallGraphNode node = graph[addr];

            node.entryaddr = addr;
            node.name = nm;
            return node;
        }

        public CallGraphNode? findNode(Address addr)
        {
            CallGraphNode? result;

            // Find function at given address, or return null
            return graph.TryGetValue(addr, out result)
                ? result ?? throw new BugException()
                : (CallGraphNode)null;
        }

        public void addEdge(CallGraphNode from, CallGraphNode to, Address addr)
        {
            int i;
            for (i = 0; i < from.outedge.Count; ++i) {
                CallGraphNode outnode = from.outedge[i].to;
                if (outnode == to) return;  // Already have an out edge
                if (to.entryaddr < outnode.entryaddr) break;
            }

            CallGraphEdge fromedge = insertBlankEdge(from, i);

            int toi = to.inedge.Count;
            CallGraphEdge toedge = new CallGraphEdge();
            to.inedge.Add(toedge);

            fromedge.from = from;
            fromedge.to = to;
            fromedge.callsiteaddr = addr;
            fromedge.complement = toi;

            toedge.from = from;
            toedge.to = to;
            toedge.callsiteaddr = addr;
            toedge.complement = i;
        }

        public void deleteInEdge(CallGraphNode node, int i)
        {
            int tosize = node.inedge.Count;
            int fromi = node.inedge[i].complement;
            CallGraphNode from = node.inedge[i].from;
            int fromsize = from.outedge.Count;

            for (int j = i + 1; j < tosize; ++j)
            {
                node.inedge[j - 1] = node.inedge[j];
                if (node.inedge[j - 1].complement >= fromi)
                    node.inedge[j - 1].complement -= 1;
            }
            node.inedge.RemoveLastItem();

            for (int j = fromi + 1; j < fromsize; ++j)
            {
                from.outedge[j - 1] = from.outedge[j];
                if (from.outedge[j - 1].complement >= i)
                    from.outedge[j - 1].complement -= 1;
            }
            from.outedge.RemoveLastItem();
        }

        public CallGraphNode? initLeafWalk()
        {
            cycleStructure();
            if (0 == seeds.Count) return (CallGraphNode)null;
            CallGraphNode node = seeds[0];
            while(true) {
                CallGraphNode pushnode = pushPossible(node, 0);
                if (pushnode == (CallGraphNode)null)
                    break;
                node = pushnode;
            }
            return node;
        }

        public CallGraphNode nextLeaf(CallGraphNode node)
        {
            int outslot;
            node = popPossible(node, out outslot);
            outslot += 1;
            while(true) {
                CallGraphNode? pushnode = pushPossible(node, outslot);
                if (pushnode == (CallGraphNode)null)
                    break;
                node = pushnode;
                outslot = 0;
            }
            return node;
        }

        public Dictionary<Address, CallGraphNode>.Enumerator begin() => graph.GetEnumerator();

        // Unused
        // public Dictionary<Address, CallGraphNode>::iterator end() => graph.end();

        public void buildAllNodes()
        {
            // Make every function symbol into a node
            iterateScopesRecursive(glb.symboltab.getGlobalScope());
        }

        public void buildEdges(Funcdata fd)
        {
            // Build edges from a disassembled (decompiled) function
            CallGraphNode fdnode = findNode(fd.getAddress())
                ?? throw new LowlevelError("Function is missing from callgraph");
            if (fd.getFuncProto().getModelExtraPop() == ProtoModel.extrapop_unknown)
                fd.fillinExtrapop();

            int numcalls = fd.numCalls();
            for (int i = 0; i < numcalls; ++i) {
                FuncCallSpecs fs = fd.getCallSpecs(i);
                Address addr = fs.getEntryAddress();
                if (!addr.isInvalid()) {
                    CallGraphNode? tonode = findNode(addr);
                    if (tonode == (CallGraphNode)null) {
                        string name;
                        glb.nameFunction(addr, out name);
                        tonode = addNode(addr, name);
                    }
                    addEdge(fdnode, tonode, fs.getOp().getAddr());
                }
            }
        }

        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_CALLGRAPH);
            foreach (CallGraphNode node in graph.Values)
                node.encode(encoder);

            // Dump all the "in" edges
            foreach (CallGraphNode node in graph.Values) {
                for (int i = 0; i < node.inedge.Count; ++i)
                    node.inedge[i].encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_CALLGRAPH);
        }

        public void decoder(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_CALLGRAPH);
            while(true) {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_EDGE)
                    CallGraphEdge.decode(decoder, this);
                else
                    CallGraphNode.decode(decoder, this);
            }
            decoder.closeElement(elemId);
        }
    }
}
