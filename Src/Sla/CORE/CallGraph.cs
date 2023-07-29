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
    internal class CallGraph
    {
        private Architecture glb;
        private Dictionary<Address, CallGraphNode> graph; // Nodes in the graph sorted by address
        private List<CallGraphNode> seeds;

        private bool findNoEntry(List<CallGraphNode> seeds)
        { // Find all functions (that are not already marked) that either have no in edges at all,
          // or have no in edges that haven't been snipped as part of cycles

            map<Address, CallGraphNode>::iterator iter;
            CallGraphNode* lownode = (CallGraphNode*)0;
            bool allcovered = true;
            bool newseeds = false;

            for (iter = graph.begin(); iter != graph.end(); ++iter)
            {
                CallGraphNode & node((*iter).second);
                if (node.isMark()) continue;
                if ((node.inedge.size() == 0) || ((node.flags & CallGraphNode::onlycyclein) != 0))
                {
                    seeds.push_back(&node);
                    node.flags |= CallGraphNode::mark | CallGraphNode::entrynode;
                    newseeds = true;
                }
                else
                {
                    allcovered = false;
                    // We need to worry about the case where everything is in a cycle, so we don't find a natural root
                    // We use the node with the lowest number of in edges as a pseudo root
                    if (lownode == (CallGraphNode*)0)
                        lownode = &node;
                    else
                    {
                        if (node.numInEdge() < lownode.numInEdge())
                            lownode = &node;
                    }
                }
            }
            if ((!newseeds) && (!allcovered))
            {
                seeds.push_back(lownode);
                lownode.flags |= CallGraphNode::mark | CallGraphNode::entrynode;
            }
            return allcovered;
        }

        private void snipCycles(CallGraphNode node)
        { // Snip any cycles starting from root -node-
            CallGraphNode* next;
            List<LeafIterator> stack;

            node.flags |= CallGraphNode::currentcycle;
            stack.push_back(LeafIterator(node));

            while (!stack.empty())
            {
                CallGraphNode* cur = stack.back().node; // Current node
                int st = stack.back().outslot; // which out edge we will follow
                if (st >= cur.outedge.size())
                {
                    cur.flags &= ~((uint)CallGraphNode::currentcycle);
                    stack.pop_back();
                }
                else
                {
                    stack.back().outslot += 1;
                    if ((cur.outedge[st].flags & CallGraphEdge::cycle) != 0) continue;
                    next = cur.outedge[st].to;
                    if ((next.flags & CallGraphNode::currentcycle) != 0)
                    { // Found a cycle
                        snipEdge(cur, st);
                        continue;
                    }
                    else if ((next.flags & CallGraphNode::mark) != 0)
                    {   // Already traced before
                        cur.outedge[st].flags |= CallGraphEdge::dontfollow;
                        continue;
                    }
                    next.parentedge = cur.outedge[st].complement;
                    next.flags |= (CallGraphNode::currentcycle | CallGraphNode::mark);
                    stack.push_back(LeafIterator(next));
                }
            }
        }

        private void snipEdge(CallGraphNode node, int i)
        {
            node.outedge[i].flags |= CallGraphEdge::cycle | CallGraphEdge::dontfollow;
            int toi = node.outedge[i].complement;
            CallGraphNode* to = node.outedge[i].to;
            to.inedge[toi].flags |= CallGraphEdge::cycle;
            bool onlycycle = true;
            for (uint j = 0; j < to.inedge.size(); ++j)
            {
                if ((to.inedge[j].flags & CallGraphEdge::cycle) == 0)
                {
                    onlycycle = false;
                    break;
                }
            }
            if (onlycycle)
                to.flags |= CallGraphNode::onlycyclein;
        }

        private void clearMarks()
        {
            map<Address, CallGraphNode>::iterator iter;

            for (iter = graph.begin(); iter != graph.end(); ++iter)
                (*iter).second.clearMark();
        }

        private void cycleStructure()
        { // Generate list of seeds nodes (from which we can get to everything)
            if (!seeds.empty())
                return;
            uint walked = 0;
            bool allcovered;

            do
            {
                allcovered = findNoEntry(seeds);
                while (walked < seeds.size())
                {
                    CallGraphNode* rootnode = seeds[walked];
                    rootnode.parentedge = walked;
                    snipCycles(rootnode);
                    walked += 1;
                }
            } while (!allcovered);
            clearMarks();
        }

        private CallGraphNode popPossible(CallGraphNode node, int outslot)
        {
            if ((node.flags & CallGraphNode::entrynode) != 0)
            {
                outslot = node.parentedge;
                return (CallGraphNode*)0;
            }
            outslot = node.inedge[node.parentedge].complement;
            return node.inedge[node.parentedge].from;
        }

        private CallGraphNode pushPossible(CallGraphNode node, int outslot)
        {
            if (node == (CallGraphNode*)0)
            {
                if (outslot >= seeds.size())
                    return (CallGraphNode*)0;
                return seeds[outslot];
            }
            while (outslot < node.outedge.size())
            {
                if ((node.outedge[outslot].flags & CallGraphEdge::dontfollow) != 0)
                    outslot += 1;
                else
                    return node.outedge[outslot].to;
            }
            return (CallGraphNode*)0;
        }

        private CallGraphEdge insertBlankEdge(CallGraphNode node, int slot)
        {
            node.outedge.emplace_back();
            if (node.outedge.size() > 1)
            {
                for (int i = node.outedge.size() - 2; i >= slot; --i)
                {
                    int newi = i + 1;
                    CallGraphEdge & edge(node.outedge[newi]);
                    edge = node.outedge[i];
                    CallGraphNode* nodeout = edge.to;
                    nodeout.inedge[edge.complement].complement += 1;
                }
            }
            return node.outedge[slot];
        }

        private void iterateScopesRecursive(Scope scope)
        {
            if (!scope.isGlobal()) return;
            iterateFunctionsAddrOrder(scope);
            ScopeMap::const_iterator iter, enditer;
            iter = scope.childrenBegin();
            enditer = scope.childrenEnd();
            for (; iter != enditer; ++iter)
            {
                iterateScopesRecursive((*iter).second);
            }
        }

        private void iterateFunctionsAddrOrder(Scope scope)
        {
            MapIterator miter, menditer;
            miter = scope.begin();
            menditer = scope.end();
            while (miter != menditer)
            {
                Symbol* sym = (*miter).getSymbol();
                FunctionSymbol* fsym = dynamic_cast<FunctionSymbol*>(sym);
                ++miter;
                if (fsym != (FunctionSymbol*)0)
                    addNode(fsym.getFunction());
            }
        }

        public CallGraph(Architecture g)
        {
            glb = g;
        }

        public CallGraphNode addNode(Funcdata f)
        { // Add a node, based on an existing function -f-
            CallGraphNode & node(graph[f.getAddress()]);

            if ((node.getFuncdata() != (Funcdata*)0) && (node.getFuncdata() != f))
                throw new LowlevelError("Functions with duplicate entry points: " + f.getName() + " " + node.getFuncdata().getName());

            node.entryaddr = f.getAddress();
            node.name = f.getDisplayName();
            node.fd = f;
            return &node;
        }

        public CallGraphNode addNode(Address addr, string nm)
        {
            CallGraphNode & node(graph[addr]);

            node.entryaddr = addr;
            node.name = nm;

            return &node;
        }

        public CallGraphNode findNode(Address addr)
        {               // Find function at given address, or return null
            map<Address, CallGraphNode>::iterator iter;

            iter = graph.find(addr);
            if (iter != graph.end())
                return &(*iter).second;
            return (CallGraphNode*)0;
        }

        public void addEdge(CallGraphNode from, CallGraphNode to, Address addr)
        {
            int i;
            for (i = 0; i < from.outedge.size(); ++i)
            {
                CallGraphNode* outnode = from.outedge[i].to;
                if (outnode == to) return;  // Already have an out edge
                if (to.entryaddr < outnode.entryaddr) break;
            }

            CallGraphEdge & fromedge(insertBlankEdge(from, i));

            int toi = to.inedge.size();
            to.inedge.emplace_back();
            CallGraphEdge & toedge(to.inedge.back());

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
            int tosize = node.inedge.size();
            int fromi = node.inedge[i].complement;
            CallGraphNode* from = node.inedge[i].from;
            int fromsize = from.outedge.size();

            for (int j = i + 1; j < tosize; ++j)
            {
                node.inedge[j - 1] = node.inedge[j];
                if (node.inedge[j - 1].complement >= fromi)
                    node.inedge[j - 1].complement -= 1;
            }
            node.inedge.pop_back();

            for (int j = fromi + 1; j < fromsize; ++j)
            {
                from.outedge[j - 1] = from.outedge[j];
                if (from.outedge[j - 1].complement >= i)
                    from.outedge[j - 1].complement -= 1;
            }
            from.outedge.pop_back();
        }

        public CallGraphNode initLeafWalk()
        {
            cycleStructure();
            if (seeds.empty()) return (CallGraphNode*)0;
            CallGraphNode* node = seeds[0];
            for (; ; )
            {
                CallGraphNode* pushnode = pushPossible(node, 0);
                if (pushnode == (CallGraphNode*)0)
                    break;
                node = pushnode;
            }
            return node;
        }

        public CallGraphNode nextLeaf(CallGraphNode node)
        {
            int outslot;
            node = popPossible(node, outslot);
            outslot += 1;
            for (; ; )
            {
                CallGraphNode* pushnode = pushPossible(node, outslot);
                if (pushnode == (CallGraphNode*)0)
                    break;
                node = pushnode;
                outslot = 0;
            }
            return node;
        }

        public Dictionary<Address, CallGraphNode>::iterator begin() => graph.begin();

        public Dictionary<Address, CallGraphNode>::iterator end() => graph.end();

        public void buildAllNodes()
        {               // Make every function symbol into a node
            iterateScopesRecursive(glb.symboltab.getGlobalScope());
        }

        public void buildEdges(Funcdata fd)
        {               // Build edges from a disassembled (decompiled) function
            CallGraphNode* fdnode = findNode(fd.getAddress());
            CallGraphNode* tonode;
            if (fdnode == (CallGraphNode*)0)
                throw new LowlevelError("Function is missing from callgraph");
            if (fd.getFuncProto().getModelExtraPop() == ProtoModel::extrapop_unknown)
                fd.fillinExtrapop();

            int numcalls = fd.numCalls();
            for (int i = 0; i < numcalls; ++i)
            {
                FuncCallSpecs* fs = fd.getCallSpecs(i);
                Address addr = fs.getEntryAddress();
                if (!addr.isInvalid())
                {
                    tonode = findNode(addr);
                    if (tonode == (CallGraphNode*)0)
                    {
                        string name;
                        glb.nameFunction(addr, name);
                        tonode = addNode(addr, name);
                    }
                    addEdge(fdnode, tonode, fs.getOp().getAddr());
                }
            }
        }

        public void encode(Encoder encoder)
        {
            map<Address, CallGraphNode>::const_iterator iter;

            encoder.openElement(ELEM_CALLGRAPH);

            for (iter = graph.begin(); iter != graph.end(); ++iter)
                (*iter).second.encode(encoder);

            // Dump all the "in" edges
            for (iter = graph.begin(); iter != graph.end(); ++iter)
            {
                CallGraphNode node = (*iter).second;

                for (uint i = 0; i < node.inedge.size(); ++i)
                    node.inedge[i].encode(encoder);
            }

            encoder.closeElement(ELEM_CALLGRAPH);
        }

        public void decoder(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_CALLGRAPH);
            for (; ; )
            {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ELEM_EDGE)
                    CallGraphEdge::decode(decoder, this);
                else
                    CallGraphNode::decode(decoder, this);
            }
            decoder.closeElement(elemId);
        }
    }
}
