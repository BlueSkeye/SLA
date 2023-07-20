﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Propagate conditional constants
    internal class ActionConditionalConst : Action
    {
        /// \brief Clear all marks on the given list of PcodeOps
        ///
        /// \param opList is the given list
        private static void clearMarks(List<PcodeOp> opList)
        {
            for (int4 i = 0; i < opList.size(); ++i)
                opList[i]->clearMark();
        }

        /// \brief Collect COPY, INDIRECT, and MULTIEQUAL ops reachable from the given Varnode, without going thru excised edges
        ///
        /// If data-flow from the Varnode does not go through excised edges and reaches the op via other MULTIEQUALs,
        /// INDIRECTs, and COPYs, the op is put in a list, and its mark is set
        /// \param vn is the given Varnode
        /// \param phiNodeEdges is the list of edges to excise
        /// \param reachable will hold the list ops that have been reached
        private static void collectReachable(Varnode vn, List<PcodeOpNode> phiNodeEdges,
            List<PcodeOp> reachable)
        {
            sort(phiNodeEdges.begin(), phiNodeEdges.end());
            int4 count = 0;
            if (vn->isWritten())
            {
                PcodeOp* op = vn->getDef();
                if (op->code() == CPUI_MULTIEQUAL)
                {
                    // Consider defining MULTIEQUAL to be "reachable" This allows flowToAlternatePath to discover
                    // a loop back to vn from the constBlock, even if no other non-constant path survives
                    op->setMark();
                    reachable.push_back(op);
                }
            }
            for (; ; )
            {
                list<PcodeOp*>::const_iterator iter;
                for (iter = vn->beginDescend(); iter != vn->endDescend(); ++iter)
                {
                    PcodeOp* op = *iter;
                    if (op->isMark()) continue;
                    OpCode opc = op->code();
                    if (opc == CPUI_MULTIEQUAL)
                    {
                        PcodeOpNode tmpOp = new PcodeOpNode(op, 0);
                        for (tmpOp.slot = 0; tmpOp.slot < op->numInput(); ++tmpOp.slot)
                        {
                            if (op->getIn(tmpOp.slot) != vn) continue;      // Find incoming slot for current Varnode
                                                                            // Don't count as flow if coming thru excised edge
                            if (!binary_search(phiNodeEdges.begin(), phiNodeEdges.end(), tmpOp)) break;
                        }
                        if (tmpOp.slot == op->numInput()) continue;     // Was the MULTIEQUAL reached
                    }
                    else if (opc != CPUI_COPY && opc != CPUI_INDIRECT)
                        continue;
                    reachable.push_back(op);
                    op->setMark();
                }
                if (count >= reachable.size()) break;
                vn = reachable[count]->getOut();
                count += 1;
            }
        }

        /// \brief Does the output of the given op reunite with the alternate flow
        ///
        /// Assuming alternate flows have been marked, follow the flow of the given op forward through
        /// MULTIEQUAL, INDIRECT, and COPY ops.  If it hits the alternate flow, return \b true.
        /// \param op is the given PcodeOp
        /// \return \b true is there is an alternate path
        private static bool flowToAlternatePath(PcodeOp op)
        {
            if (op->isMark()) return true;
            vector<Varnode*> markSet;
            Varnode* vn = op->getOut();
            markSet.push_back(vn);
            vn->setMark();
            int4 count = 0;
            bool foundPath = false;
            while (count < markSet.size())
            {
                vn = markSet[count];
                count += 1;
                list<PcodeOp*>::const_iterator iter;
                for (iter = vn->beginDescend(); iter != vn->endDescend(); ++iter)
                {
                    PcodeOp* nextOp = *iter;
                    OpCode opc = nextOp->code();
                    if (opc == CPUI_MULTIEQUAL)
                    {
                        if (nextOp->isMark())
                        {
                            foundPath = true;
                            break;
                        }
                    }
                    else if (opc != CPUI_COPY && opc != CPUI_INDIRECT)
                        continue;
                    Varnode* outVn = nextOp->getOut();
                    if (outVn->isMark()) continue;
                    outVn->setMark();
                    markSet.push_back(outVn);
                }
                if (foundPath) break;
            }
            for (int4 i = 0; i < markSet.size(); ++i)
                markSet[i]->clearMark();
            return foundPath;
        }

        /// \brief Test if flow from a specific edge is disjoint from other edges
        ///
        /// All MULTIEQUAL and COPY ops reachable from the edge are marked. If any other edge
        /// is in this marked set, mark both edges in the result set.
        /// \param edges is the set of edges
        /// \param i is the index of the specific edge to test
        /// \param result is the array of marks to be returned
        /// \return \b true if the selected edge flows together with any other edge
        private static bool flowTogether(List<PcodeOpNode> edges, int i, List<int> result)
        {
            vector<PcodeOp*> reachable;
            vector<PcodeOpNode> excise; // No edge excised
            collectReachable(edges[i].op->getOut(), excise, reachable);
            bool res = false;
            for (int4 j = 0; j < edges.size(); ++j)
            {
                if (i == j) continue;
                if (result[j] == 0) continue;   // Check for disconnected path
                if (edges[j].op->isMark())
                {
                    result[i] = 2;          // Disconnected paths, which flow together
                    result[j] = 2;
                    res = true;
                }
            }
            clearMarks(reachable);
            return res;
        }

        /// \brief Place a COPY of a constant at the end of a basic block
        ///
        /// \param op is an alternate "last" op
        /// \param bl is the basic block
        /// \param constVn is the constant to be assigned
        /// \param data is the function containing the block
        /// \return the new output Varnode of the COPY
        private static Varnode placeCopy(PcodeOp op, BlockBasic bl, Varnode constVn, Funcdata data)
        {
            PcodeOp* lastOp = bl->lastOp();
            list<PcodeOp*>::iterator iter;
            Address addr;
            if (lastOp == (PcodeOp*)0)
            {
                iter = bl->endOp();
                addr = op->getAddr();
            }
            else if (lastOp->isBranch())
            {
                iter = lastOp->getBasicIter();  // Insert before any branch
                addr = lastOp->getAddr();
            }
            else
            {
                iter = bl->endOp();
                addr = lastOp->getAddr();
            }
            PcodeOp* copyOp = data.newOp(1, addr);
            data.opSetOpcode(copyOp, CPUI_COPY);
            Varnode* outVn = data.newUniqueOut(constVn->getSize(), copyOp);
            data.opSetInput(copyOp, constVn, 0);
            data.opInsert(copyOp, bl, iter);
            return outVn;
        }

        /// \brief Place a single COPY assignment shared by multiple MULTIEQUALs
        ///
        /// Find the common ancestor block among all MULTIEQUALs marked as flowing together.
        /// Place a COPY assigning a constant at the bottom of this block.
        /// Replace all the input edge Varnodes on the MULTIEQUALs with the output of this COPY.
        /// \param phiNodeEdges is the list of MULTIEQUALs and their incoming edges
        /// \param marks are the marks applied to the MULTIEQUALs (2 == flowtogether)
        /// \param constVn is the constant being assigned by the COPY
        /// \param data is the function
        private static void placeMultipleConstants(List<PcodeOpNode> phiNodeEdges,
            List<int> marks, Varnode constVn, Funcdata data)
        {
            vector<FlowBlock*> blocks;
            PcodeOp* op = (PcodeOp*)0;
            for (int4 i = 0; i < phiNodeEdges.size(); ++i)
            {
                if (marks[i] != 2) continue;    // Check that the MULTIQUAL is marked as flowing together
                op = phiNodeEdges[i].op;
                FlowBlock* bl = op->getParent();
                bl = bl->getIn(phiNodeEdges[i].slot);
                blocks.push_back(bl);
            }
            BlockBasic* rootBlock = (BlockBasic*)FlowBlock::findCommonBlock(blocks);
            Varnode* outVn = placeCopy(op, rootBlock, constVn, data);
            for (int4 i = 0; i < phiNodeEdges.size(); ++i)
            {
                if (marks[i] != 2) continue;
                data.opSetInput(phiNodeEdges[i].op, outVn, phiNodeEdges[i].slot);
            }
        }

        /// \brief Replace MULTIEQUAL edges with constant if there is no alternate flow
        ///
        /// A given Varnode is known to be constant along a set of MULTIEQUAL edges. If these edges are excised from the
        /// data-flow, and the output of a MULTIEQUAL does not rejoin with the Varnode along an alternate path, then that
        /// edge is replaced with a constant.
        /// \param varVn is the given Varnode
        /// \param constVn is the constant to replace it with
        /// \param phiNodeEdges is the set of edges the Varnode is known to be constant on
        /// \param data is the function containing this data-flow
        private void handlePhiNodes(Varnode varVn, Varnode constVn, List<PcodeOpNode> phiNodeEdges,
            Funcdata data)
        {
            vector<PcodeOp*> alternateFlow;
            vector<int4> results(phiNodeEdges.size(),0);
            collectReachable(varVn, phiNodeEdges, alternateFlow);
            int4 alternate = 0;
            for (int4 i = 0; i < phiNodeEdges.size(); ++i)
            {
                if (!flowToAlternatePath(phiNodeEdges[i].op))
                {
                    results[i] = 1; // Mark as disconnecting
                    alternate += 1;
                }
            }
            clearMarks(alternateFlow);

            bool hasFlowTogether = false;
            if (alternate > 1)
            {
                // If we reach here, multiple MULTIEQUAL are disjoint from the non-constant flow
                for (int4 i = 0; i < results.size(); ++i)
                {
                    if (results[i] == 0) continue;      // Is this a disconnected path
                    if (flowTogether(phiNodeEdges, i, results)) // Check if the disconnected paths flow together
                        hasFlowTogether = true;
                }
            }
            // Add COPY assignment for each edge that has its own disconnected path going forward
            for (int4 i = 0; i < phiNodeEdges.size(); ++i)
            {
                if (results[i] != 1) continue;      // Check for disconnected path that does not flow into another path
                PcodeOp* op = phiNodeEdges[i].op;
                int4 slot = phiNodeEdges[i].slot;
                BlockBasic* bl = (BlockBasic*)op->getParent()->getIn(slot);
                Varnode* outVn = placeCopy(op, bl, constVn, data);
                data.opSetInput(op, outVn, slot);
                count += 1;
            }
            if (hasFlowTogether)
            {
                placeMultipleConstants(phiNodeEdges, results, constVn, data);   // Add COPY assignment for edges that flow together
                count += 1;
            }
        }

        /// \brief Replace reads of a given Varnode with a constant.
        ///
        /// For each read op, check that is in or dominated by a specific block we known
        /// the Varnode is constant in.
        /// \param varVn is the given Varnode
        /// \param constVn is the constant Varnode to replace with
        /// \param constBlock is the block which dominates ops reading the constant value
        /// \param useMultiequal is \b true if conditional constants can be applied to MULTIEQUAL ops
        /// \param data is the function being analyzed
        private void propagateConstant(Varnode varVn, Varnode constVn, FlowBlock constBlock,
            bool useMultiequal, Funcdata data)
        {
            vector<PcodeOpNode> phiNodeEdges;
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = varVn->beginDescend();
            enditer = varVn->endDescend();
            while (iter != enditer)
            {
                PcodeOp* op = *iter;
                while (iter != enditer && *iter == op)
                    ++iter;             // Advance iterator off of current op, as this descendant may be erased
                OpCode opc = op->code();
                if (opc == CPUI_INDIRECT)           // Don't propagate constant into these
                    continue;
                else if (opc == CPUI_MULTIEQUAL)
                {
                    if (!useMultiequal)
                        continue;
                    if (varVn->isAddrTied() && varVn->getAddr() == op->getOut()->getAddr())
                        continue;
                    FlowBlock* bl = op->getParent();
                    for (int4 slot = 0; slot < op->numInput(); ++slot)
                    {
                        if (op->getIn(slot) == varVn)
                        {
                            if (constBlock->dominates(bl->getIn(slot)))
                            {
                                phiNodeEdges.emplace_back(op, slot);
                            }
                        }
                    }
                    continue;
                }
                else if (opc == CPUI_COPY)
                {       // Don't propagate into COPY unless...
                    PcodeOp* followOp = op->getOut()->loneDescend();
                    if (followOp == (PcodeOp*)0) continue;
                    if (followOp->isMarker()) continue;
                    if (followOp->code() == CPUI_COPY) continue;
                    // ...unless COPY is into something more interesting
                }
                if (constBlock->dominates(op->getParent()))
                {
                    int4 slot = op->getSlot(varVn);
                    data.opSetInput(op, constVn, slot); // Replace ref with constant!
                    count += 1;         // We made a change
                }
            }
            if (!phiNodeEdges.empty())
                handlePhiNodes(varVn, constVn, phiNodeEdges, data);
        }

        public ActionConditionalConst(string g)
            : base(0,"condconst", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionConditionalConst(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            bool useMultiequal = true;
            AddrSpace* stackSpace = data.getArch()->getStackSpace();
            if (stackSpace != (AddrSpace*)0)
            {
                // Determining if conditional constants should apply to MULTIEQUAL operations may require
                // flow calculations.
                int4 numPasses = data.numHeritagePasses(stackSpace);
                if (numPasses <= 0)     // If the stack hasn't been heritaged yet
                    useMultiequal = false;  // Don't propagate into MULTIEQUAL
            }
            const BlockGraph &blockGraph(data.getBasicBlocks());
            for (int4 i = 0; i < blockGraph.getSize(); ++i)
            {
                FlowBlock* bl = blockGraph.getBlock(i);
                PcodeOp* cBranch = bl->lastOp();
                if (cBranch == (PcodeOp*)0 || cBranch->code() != CPUI_CBRANCH) continue;
                Varnode* boolVn = cBranch->getIn(1);
                if (!boolVn->isWritten()) continue;
                PcodeOp* compOp = boolVn->getDef();
                OpCode opc = compOp->code();
                bool flipEdge = cBranch->isBooleanFlip();
                if (opc == CPUI_BOOL_NEGATE)
                {
                    flipEdge = !flipEdge;
                    boolVn = compOp->getIn(0);
                    if (!boolVn->isWritten()) continue;
                    compOp = boolVn->getDef();
                    opc = compOp->code();
                }
                int4 constEdge;         // Out edge where value is constant
                if (opc == CPUI_INT_EQUAL)
                    constEdge = 1;
                else if (opc == CPUI_INT_NOTEQUAL)
                    constEdge = 0;
                else
                    continue;
                // Find the variable and verify that it is compared to a constant
                Varnode* varVn = compOp->getIn(0);
                Varnode* constVn = compOp->getIn(1);
                if (!constVn->isConstant())
                {
                    if (!varVn->isConstant())
                        continue;
                    Varnode* tmp = constVn;
                    constVn = varVn;
                    varVn = tmp;
                }
                if (flipEdge)
                    constEdge = 1 - constEdge;
                FlowBlock* constBlock = bl->getOut(constEdge);
                if (!constBlock->restrictedByConditional(bl)) continue; // Make sure condition holds
                propagateConstant(varVn, constVn, constBlock, useMultiequal, data);
            }
            return 0;
        }
    }
}