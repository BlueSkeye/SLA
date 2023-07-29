﻿using ghidra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class that determines a ValueSet for each Varnode in a data-flow system
    ///
    /// This class uses \e value \e set \e analysis to calculate (an overestimation of)
    /// the range of values that can reach each Varnode.  The system is formed by providing
    /// a set of Varnodes for which the range is desired (the sinks) via establishValueSets().
    /// This creates a system of Varnodes (within the single function) that can flow to the sinks.
    /// Running the method solve() does the analysis, and the caller can examine the results
    /// by examining the ValueSet attached to any of the Varnodes in the system (via Varnode::getValueSet()).
    /// The ValueSetSolver::solve() starts with minimal value sets and does iteration steps by pushing
    /// them through the PcodeOps until stability is reached. A Widener object is passed to solve()
    /// which selects the specific strategy for accelerating convergence.
    internal class ValueSetSolver
    {
        /// \brief An iterator over out-bound edges for a single ValueSet node in a data-flow system
        ///
        /// This is a helper class for walking a collection of ValueSets as a graph.
        /// Mostly the graph mirrors the data-flow of the Varnodes underlying the ValueSets, but
        /// there is support for a simulated root node. This class acts as an iterator over the outgoing
        /// edges of a particular ValueSet in the graph.
        internal class ValueSetEdge
        {
            /// The list of nodes attached to the simulated root node (or NULL)
            private List<ValueSet> rootEdges;
            /// The iterator position for the simulated root node
            private int4 rootPos;
            /// The Varnode attached to a normal ValueSet node (or NULL)
            private Varnode vn;
            /// The iterator position for a normal ValueSet node
            private IEnumerator<PcodeOp> iter;

            /// \brief Construct an iterator over the outbound edges of the given ValueSet node
            ///
            /// Mostly this just forwards the ValueSets attached to output Varnodes
            /// of the descendant ops of the Varnode attached to the given node, but this
            /// allows for an artificial root node so we can simulate multiple input nodes.
            /// \param node is the given ValueSet node (NULL if this is the simulated root)
            /// \param roots is the list of input ValueSets to use for the simulated root
            public ValueSetEdge(ValueSet node, List<ValueSet> roots)
            {
                vn = node->getVarnode();
                if (vn == (Varnode*)0)
                {       // Assume this is the simulated root
                    rootEdges = &roots;         // Set up for simulated edges
                    rootPos = 0;
                }
                else
                {
                    rootEdges = (const vector<ValueSet*>*)0;
                    iter = vn->beginDescend();
                }
            }

            /// \brief Get the ValueSet pointed to by this iterator and advance the iterator
            ///
            /// This method assumes all Varnodes with an attached ValueSet have been marked.
            /// \return the next ValueSet or NULL if the end of the list is reached
            public ValueSet getNext()
            {
                if (vn == (Varnode*)0)
                {
                    if (rootPos < rootEdges->size())
                    {
                        ValueSet* res = (*rootEdges)[rootPos];
                        rootPos += 1;
                        return res;
                    }
                    return (ValueSet*)0;
                }
                while (iter != vn->endDescend())
                {
                    PcodeOp* op = *iter;
                    ++iter;
                    Varnode* outVn = op->getOut();
                    if (outVn != (Varnode*)0 && outVn->isMark())
                    {
                        return outVn->getValueSet();
                    }
                }
                return (ValueSet*)0;
            }
        }

        /// Storage for all the current value sets
        private List<ValueSet> valueNodes;
        /// Additional, after iteration, add-on value sets
        private Dictionary<SeqNum, ValueSetRead> readNodes;
        /// Value sets in iteration order
        private Partition orderPartition;
        /// Storage for the Partitions establishing components
        private List<Partition> recordStorage;
        /// Values treated as inputs
        private List<ValueSet> rootNodes;
        /// Stack used to generate the topological ordering
        private List<ValueSet> nodeStack;
        /// (Global) depth first numbering for topological ordering
        private int4 depthFirstIndex;
        /// Count of individual ValueSet iterations
        private int4 numIterations;
        /// Maximum number of iterations before forcing termination
        private int4 maxIterations;

        /// Allocate storage for a new ValueSet
        /// The new ValueSet is attached to the given Varnode
        /// \param vn is the given Varnode
        /// \param tCode is the type to associate with the Varnode
        private void newValueSet(Varnode vn, int4 tCode)
        {
            valueNodes.emplace_back();
            valueNodes.back().setVarnode(vn, tCode);
        }

        /// Prepend a vertex to a partition
        /// \param vertex is the node that will be prepended
        /// \param part is the Partition being modified
        private static void partitionPrepend(ValueSet vertex, Partition part)
        {
            vertex->next = part.startNode;  // Attach new vertex to beginning of list
            part.startNode = vertex;        // Change the first value set to be the new vertex
            if (part.stopNode == (ValueSet*)0)
                part.stopNode = vertex;
        }

        /// Prepend full Partition to given Partition
        /// \param head is the partition to be prepended
        /// \param part is the given partition being modified (prepended to)
        private static void partitionPrepend(Partition head,Partition part)
        {
            head.stopNode->next = part.startNode;
            part.startNode = head.startNode;
            if (part.stopNode == (ValueSet*)0)
                part.stopNode = head.stopNode;
        }

        /// Create a full partition component
        /// This method saves a Partition to permanent storage. It marks the
        /// starting node of the partition and sets up for the iterating algorithm.
        /// \param part is the partition to store
        private void partitionSurround(Partition part)
        {
            recordStorage.push_back(part);
            part.startNode->partHead = &recordStorage.back();
        }

        /// Generate a partition component given its head
        /// Knowing that the given Varnode is the head of a partition, generate
        /// the partition recursively and generate the formal Partition object.
        /// \param vertex is the given ValueSet (attached to the head Varnode)
        /// \param part will hold the constructed Partition
        private void component(ValueSet vertex, Partition part)
        {
            ValueSetEdge edgeIterator(vertex, rootNodes);
            ValueSet* succ = edgeIterator.getNext();
            while (succ != (ValueSet*)0)
            {
                if (succ->count == 0)
                    visit(succ, part);
                succ = edgeIterator.getNext();
            }
            partitionPrepend(vertex, part);
            partitionSurround(part);
        }

        /// Recursively walk the data-flow graph finding partitions
        /// \param vertex is the current Varnode being walked
        /// \param part is the current Partition being constructed
        /// \return the index of calculated head ValueSet for the current Parition
        private int4 visit(ValueSet vertex, Partition part)
        {
            nodeStack.push_back(vertex);
            depthFirstIndex += 1;
            vertex->count = depthFirstIndex;
            int4 head = depthFirstIndex;
            bool loop = false;
            ValueSetEdge edgeIterator(vertex, rootNodes);
            ValueSet* succ = edgeIterator.getNext();
            while (succ != (ValueSet*)0)
            {
                int4 min;
                if (succ->count == 0)
                    min = visit(succ, part);
                else
                    min = succ->count;
                if (min <= head)
                {
                    head = min;
                    loop = true;
                }
                succ = edgeIterator.getNext();
            }
            if (head == vertex->count)
            {
                vertex->count = 0x7fffffff; // Set to "infinity"
                ValueSet* element = nodeStack.back();
                nodeStack.pop_back();
                if (loop)
                {
                    while (element != vertex)
                    {
                        element->count = 0;
                        element = nodeStack.back();
                        nodeStack.pop_back();
                    }
                    Partition compPart;         // empty partition
                    component(vertex, compPart);
                    partitionPrepend(compPart, part);
                }
                else
                {
                    partitionPrepend(vertex, part);
                }
            }
            return head;
        }

        /// Find the optimal order for iterating through the ValueSets
        /// \brief Establish the recursive node ordering for iteratively solving the value set system.
        ///
        /// This algorithm is based on "Efficient chaotic iteration strategies with widenings" by
        /// Francois Bourdoncle.  The Varnodes in the system are ordered and a set of nested
        /// Partition components are generated.  Iterating the ValueSets proceeds in this order,
        /// looping through the components recursively until a fixed point is reached.
        /// This implementation assumes all Varnodes in the system are distinguished by
        /// Varnode::isMark() returning \b true.
        private void establishTopologicalOrder()
        {
            for (list<ValueSet>::iterator iter = valueNodes.begin(); iter != valueNodes.end(); ++iter)
            {
                (*iter).count = 0;
                (*iter).next = (ValueSet*)0;
                (*iter).partHead = (Partition*)0;
            }
            ValueSet rootNode;
            rootNode.vn = (Varnode*)0;
            depthFirstIndex = 0;
            visit(&rootNode, orderPartition);
            orderPartition.startNode = orderPartition.startNode->next;  // Remove simulated root
        }

        /// \brief Generate an equation given a \b true constraint and the input/output Varnodes it affects
        ///
        /// The equation is expressed as: only \b true values can reach the indicated input to a specific PcodeOp.
        /// The equation is attached to the output of the PcodeOp.
        /// \param vn is the output Varnode the equation will be attached to
        /// \param op is the specific PcodeOp
        /// \param slot is the input slot of the constrained input Varnode
        /// \param type is the type of values
        /// \param range is the range of \b true values
        private void generateTrueEquation(Varnode vn, PcodeOp op, int4 slot, int4 type, CircleRange range)
        {
            if (vn != (Varnode*)0)
                vn->getValueSet()->addEquation(slot, type, range);
            else
                readNodes[op->getSeqNum()].addEquation(slot, type, range);// Special read site
        }

        /// \brief Generate the complementary equation given a \b true constraint and the input/output Varnodes it affects
        ///
        /// The equation is expressed as: only \b false values can reach the indicated input to a specific PcodeOp.
        /// The equation is attached to the output of the PcodeOp.
        /// \param vn is the output Varnode the equation will be attached to
        /// \param op is the specific PcodeOp
        /// \param slot is the input slot of the constrained input Varnode
        /// \param type is the type of values
        /// \param range is the range of \b true values, which must be complemented
        private void generateFalseEquation(Varnode vn, PcodeOp op, int4 slot, int4 type, CircleRange range)
        {
            CircleRange falseRange(range);
            falseRange.invert();
            if (vn != (Varnode*)0)
                vn->getValueSet()->addEquation(slot, type, falseRange);
            else
                readNodes[op->getSeqNum()].addEquation(slot, type, falseRange);// Special read site
        }

        /// \brief Look for PcodeOps where the given constraint range applies and instantiate an equation
        ///
        /// If a read of the given Varnode is in a basic block dominated by the condition producing the
        /// constraint, then either the constraint or its complement applies to the PcodeOp reading
        /// the Varnode.  An equation holding the constraint is added to the ValueSet of the Varnode
        /// output of the PcodeOp.
        /// \param vn is the given Varnode
        /// \param type is the constraint characteristic
        /// \param range is the known constraint (assuming the \b true branch was taken)
        /// \param cbranch is conditional branch creating the constraint
        private void applyConstraints(Varnode vn, int4 type, CircleRange range,PcodeOp cbranch)
        {
            FlowBlock* splitPoint = cbranch->getParent();
            FlowBlock* trueBlock,*falseBlock;
            if (cbranch->isBooleanFlip())
            {
                trueBlock = splitPoint->getFalseOut();
                falseBlock = splitPoint->getTrueOut();
            }
            else
            {
                trueBlock = splitPoint->getTrueOut();
                falseBlock = splitPoint->getFalseOut();
            }
            // Check if the only path to trueBlock or falseBlock is via a splitPoint out-edge induced by the condition
            bool trueIsRestricted = trueBlock->restrictedByConditional(splitPoint);
            bool falseIsRestricted = falseBlock->restrictedByConditional(splitPoint);

            list<PcodeOp*>::const_iterator iter;
            if (vn->isWritten())
            {
                ValueSet* vSet = vn->getValueSet();
                if (vSet->opCode == CPUI_MULTIEQUAL)
                {
                    vSet->addLandmark(type, range);     // Leave landmark for widening
                }
            }
            for (iter = vn->beginDescend(); iter != vn->endDescend(); ++iter)
            {
                PcodeOp* op = *iter;
                Varnode* outVn = (Varnode*)0;
                if (!op->isMark())
                {   // If this is not a special read site
                    outVn = op->getOut();   // Make sure there is a Varnode in the system
                    if (outVn == (Varnode*)0) continue;
                    if (!outVn->isMark()) continue;
                }
                FlowBlock* curBlock = op->getParent();
                int4 slot = op->getSlot(vn);
                if (op->code() == CPUI_MULTIEQUAL)
                {
                    if (curBlock == trueBlock)
                    {
                        // If its possible that both the true and false edges can reach trueBlock
                        // then the only input we can restrict is a MULTIEQUAL input along the exact true edge
                        if (trueIsRestricted || trueBlock->getIn(slot) == splitPoint)
                            generateTrueEquation(outVn, op, slot, type, range);
                        continue;
                    }
                    else if (curBlock == falseBlock)
                    {
                        // If its possible that both the true and false edges can reach falseBlock
                        // then the only input we can restrict is a MULTIEQUAL input along the exact false edge
                        if (falseIsRestricted || falseBlock->getIn(slot) == splitPoint)
                            generateFalseEquation(outVn, op, slot, type, range);
                        continue;
                    }
                    else
                        curBlock = curBlock->getIn(slot);   // MULTIEQUAL input is really only from one in-block
                }
                for (; ; )
                {
                    if (curBlock == trueBlock)
                    {
                        if (trueIsRestricted)
                            generateTrueEquation(outVn, op, slot, type, range);
                        break;
                    }
                    else if (curBlock == falseBlock)
                    {
                        if (falseIsRestricted)
                            generateFalseEquation(outVn, op, slot, type, range);
                        break;
                    }
                    else if (curBlock == splitPoint || curBlock == (FlowBlock*)0)
                        break;
                    curBlock = curBlock->getImmedDom();
                }
            }
        }

        /// \brief Generate constraints given a Varnode path
        ///
        /// Knowing that there is a lifting path from the given starting Varnode to an ending Varnode
        /// in the system, go ahead and lift the given range to a final constraint on the ending
        /// Varnode.  Then look for reads of the Varnode where the constraint applies.
        /// \param type is the constraint characteristic
        /// \param lift is the given range that will be lifted
        /// \param startVn is the starting Varnode
        /// \param endVn is the given ending Varnode in the system
        /// \param cbranch is the PcodeOp causing the control-flow split
        private void constraintsFromPath(int4 type, CircleRange lift, Varnode startVn, Varnode endVn,
            PcodeOp cbranch)
        {
            while (startVn != endVn)
            {
                Varnode* constVn;
                startVn = lift.pullBack(startVn->getDef(), &constVn, false);
                if (startVn == (Varnode*)0) return; // Couldn't pull all the way back to our value set
            }
            for (; ; )
            {
                Varnode* constVn;
                applyConstraints(endVn, type, lift, cbranch);
                if (!endVn->isWritten()) break;
                PcodeOp* op = endVn->getDef();
                if (op->isCall() || op->isMarker()) break;
                endVn = lift.pullBack(op, &constVn, false);
                if (endVn == (Varnode*)0) break;
                if (!endVn->isMark()) break;
            }
        }

        /// Generate constraints arising from the given branch
        /// Lift the set of values on the condition for the given CBRANCH to any
        /// Varnode in the system, and label (the reads) of any such Varnode with
        /// the constraint. If the values cannot be lifted or no Varnode in the system
        /// is found, no constraints are generated.
        /// \param cbranch is the given condition branch
        private void constraintsFromCBranch(PcodeOp cbranch)
        {
            Varnode* vn = cbranch->getIn(1); // Get Varnode deciding the condition
            while (!vn->isMark())
            {
                if (!vn->isWritten()) break;
                PcodeOp* op = vn->getDef();
                if (op->isCall() || op->isMarker())
                    break;
                int4 num = op->numInput();
                if (num == 0 || num > 2) break;
                vn = op->getIn(0);
                if (num == 2)
                {
                    if (vn->isConstant())
                        vn = op->getIn(1);
                    else if (!op->getIn(1)->isConstant())
                    {
                        // If we reach here, both inputs are non-constant
                        generateRelativeConstraint(op, cbranch);
                        return;
                    }
                    // If we reach here, vn is non-constant, other input is constant
                }
            }
            if (vn->isMark())
            {
                CircleRange lift(true);
                Varnode* startVn = cbranch->getIn(1);
                constraintsFromPath(0, lift, startVn, vn, cbranch);
            }
        }

        /// Generate constraints given a system of Varnodes
        /// Given a complete data-flow system of Varnodes, look for any \e constraint:
        ///   - For a particular Varnode
        ///   - A limited set of values
        ///   - Due to its involvement in a branch condition
        ///   - Which applies at a particular \e read of the Varnode
        ///
        /// \param worklist is the set of Varnodes in the data-flow system (all marked)
        /// \param reads is the additional set of PcodeOps that read a Varnode from the system
        private void generateConstraints(List<Varnode> worklist, List<PcodeOp> reads)
        {
            vector<FlowBlock*> blockList;
            // Collect all blocks that contain a system op (input) or dominate a container
            for (int4 i = 0; i < worklist.size(); ++i)
            {
                PcodeOp* op = worklist[i]->getDef();
                if (op == (PcodeOp*)0) continue;
                FlowBlock* bl = op->getParent();
                if (op->code() == CPUI_MULTIEQUAL)
                {
                    for (int4 j = 0; j < bl->sizeIn(); ++j)
                    {
                        FlowBlock* curBl = bl->getIn(j);
                        do
                        {
                            if (curBl->isMark()) break;
                            curBl->setMark();
                            blockList.push_back(curBl);
                            curBl = curBl->getImmedDom();
                        } while (curBl != (FlowBlock*)0);
                    }
                }
                else
                {
                    do
                    {
                        if (bl->isMark()) break;
                        bl->setMark();
                        blockList.push_back(bl);
                        bl = bl->getImmedDom();
                    } while (bl != (FlowBlock*)0);
                }
            }
            for (int4 i = 0; i < reads.size(); ++i)
            {
                FlowBlock* bl = reads[i]->getParent();
                do
                {
                    if (bl->isMark()) break;
                    bl->setMark();
                    blockList.push_back(bl);
                    bl = bl->getImmedDom();
                } while (bl != (FlowBlock*)0);
            }
            for (int4 i = 0; i < blockList.size(); ++i)
                blockList[i]->clearMark();

            vector<FlowBlock*> finalList;
            // Now go through input blocks to the previously calculated blocks
            for (int4 i = 0; i < blockList.size(); ++i)
            {
                FlowBlock* bl = blockList[i];
                for (int4 j = 0; j < bl->sizeIn(); ++j)
                {
                    BlockBasic* splitPoint = (BlockBasic*)bl->getIn(j);
                    if (splitPoint->isMark()) continue;
                    if (splitPoint->sizeOut() != 2) continue;
                    PcodeOp* lastOp = splitPoint->lastOp();
                    if (lastOp != (PcodeOp*)0 && lastOp->code() == CPUI_CBRANCH)
                    {
                        splitPoint->setMark();
                        finalList.push_back(splitPoint);
                        constraintsFromCBranch(lastOp);     // Try to generate constraints from this splitPoint
                    }
                }
            }
            for (int4 i = 0; i < finalList.size(); ++i)
                finalList[i]->clearMark();
        }

        /// Check if the given Varnode is a \e relative constant
        /// Verify that the given Varnode is produced by a straight line sequence of
        /// COPYs, INT_ADDs with a constant, from the base register marked as \e relative
        /// for our system.
        /// \param vn is the given Varnode
        /// \param typeCode will hold the base register code (if found)
        /// \param value will hold the additive value relative to the base register (if found)
        /// \return \b true if the Varnode is a \e relative constant
        private bool checkRelativeConstant(Varnode vn, int4 typeCode, uintb value)
        {
            value = 0;
            for (; ; )
            {
                if (vn->isMark())
                {
                    ValueSet* valueSet = vn->getValueSet();
                    if (valueSet->typeCode != 0)
                    {
                        typeCode = valueSet->typeCode;
                        break;
                    }
                }
                if (!vn->isWritten()) return false;
                PcodeOp* op = vn->getDef();
                OpCode opc = op->code();
                if (opc == CPUI_COPY || opc == CPUI_INDIRECT)
                    vn = op->getIn(0);
                else if (opc == CPUI_INT_ADD || opc == CPUI_PTRSUB)
                {
                    Varnode* constVn = op->getIn(1);
                    if (!constVn->isConstant())
                        return false;
                    value = (value + constVn->getOffset()) & calc_mask(constVn->getSize());
                    vn = op->getIn(0);
                }
                else
                    return false;
            }
            return true;
        }

        /// Try to find a \e relative constraint
        /// Given a binary PcodeOp producing a conditional branch, check if it can be interpreted
        /// as a constraint relative to (the) base register specified for this system. If it can
        /// be, a \e relative Equation is generated, which will apply to \e relative ValueSets.
        /// \param compOp is the comparison PcodeOp
        /// \param cbranch is the conditional branch
        private void generateRelativeConstraint(PcodeOp compOp, PcodeOp cbranch)
        {
            OpCode opc = compOp->code();
            switch (opc)
            {
                case CPUI_INT_LESS:
                    opc = CPUI_INT_SLESS;   // Treat unsigned pointer comparisons as signed relative to the base register
                    break;
                case CPUI_INT_LESSEQUAL:
                    opc = CPUI_INT_SLESSEQUAL;
                    break;
                case CPUI_INT_SLESS:
                case CPUI_INT_SLESSEQUAL:
                case CPUI_INT_EQUAL:
                case CPUI_INT_NOTEQUAL:
                    break;
                default:
                    return;
            }
            int4 typeCode;
            uintb value;
            Varnode* vn;
            Varnode* inVn0 = compOp->getIn(0);
            Varnode* inVn1 = compOp->getIn(1);
            CircleRange lift(true);
            if (checkRelativeConstant(inVn0, typeCode, value))
            {
                vn = inVn1;
                if (!lift.pullBackBinary(opc, value, 1, vn->getSize(), 1))
                    return;
            }
            else if (checkRelativeConstant(inVn1, typeCode, value))
            {
                vn = inVn0;
                if (!lift.pullBackBinary(opc, value, 0, vn->getSize(), 1))
                    return;
            }
            else
                return;     // Neither side looks like a relative constant

            Varnode* endVn = vn;
            while (!endVn->isMark())
            {
                if (!endVn->isWritten()) return;
                PcodeOp* op = endVn->getDef();
                opc = op->code();
                if (opc == CPUI_COPY || opc == CPUI_PTRSUB)
                {
                    endVn = op->getIn(0);
                }
                else if (opc == CPUI_INT_ADD)
                {   // Can pull-back through INT_ADD
                    if (!op->getIn(1)->isConstant())    // if second param is constant
                        return;
                    endVn = op->getIn(0);
                }
                else
                    return;
            }
            constraintsFromPath(typeCode, lift, vn, endVn, cbranch);
        }

        /// \brief Build value sets for a data-flow system
        ///
        /// Given a set of sinks, find all the Varnodes that flow directly into them and set up their
        /// initial ValueSet objects.
        /// \param sinks is the list terminating Varnodes
        /// \param reads are add-on PcodeOps where we would like to know input ValueSets at the point of read
        /// \param stackReg (if non-NULL) gives the stack pointer (for keeping track of relative offsets)
        /// \param indirectAsCopy is \b true if solver should treat CPUI_INDIRECT as CPUI_COPY operations
        public void establishValueSets(List<Varnode> sinks, List<PcodeOp> reads, Varnode stackReg,
            bool indirectAsCopy)
        {
            vector<Varnode*> worklist;
            int4 workPos = 0;
            if (stackReg != (Varnode*)0)
            {
                newValueSet(stackReg, 1);       // Establish stack pointer as special
                stackReg->setMark();
                worklist.push_back(stackReg);
                workPos += 1;
                rootNodes.push_back(stackReg->getValueSet());
            }
            for (int4 i = 0; i < sinks.size(); ++i)
            {
                Varnode* vn = sinks[i];
                newValueSet(vn, 0);
                vn->setMark();
                worklist.push_back(vn);
            }
            while (workPos < worklist.size())
            {
                Varnode* vn = worklist[workPos];
                workPos += 1;
                if (!vn->isWritten())
                {
                    if (vn->isConstant())
                    {
                        // Constant inputs to binary ops should not be treated as root nodes as they
                        // get picked up during iteration by the other input, except in the case of a
                        // a PTRSUB from a spacebase constant.
                        if (vn->isSpacebase() || vn->loneDescend()->numInput() == 1)
                            rootNodes.push_back(vn->getValueSet());
                    }
                    else
                        rootNodes.push_back(vn->getValueSet());
                    continue;
                }
                PcodeOp* op = vn->getDef();
                switch (op->code())
                {   // Distinguish ops where we can never predict an integer range
                    case CPUI_INDIRECT:
                        if (indirectAsCopy || op->isIndirectStore())
                        {
                            Varnode* inVn = op->getIn(0);
                            if (!inVn->isMark())
                            {
                                newValueSet(inVn, 0);
                                inVn->setMark();
                                worklist.push_back(inVn);
                            }
                        }
                        else
                        {
                            vn->getValueSet()->setFull();
                            rootNodes.push_back(vn->getValueSet());
                        }
                        break;
                    case CPUI_CALL:
                    case CPUI_CALLIND:
                    case CPUI_CALLOTHER:
                    case CPUI_LOAD:
                    case CPUI_NEW:
                    case CPUI_SEGMENTOP:
                    case CPUI_CPOOLREF:
                    case CPUI_FLOAT_ADD:
                    case CPUI_FLOAT_DIV:
                    case CPUI_FLOAT_MULT:
                    case CPUI_FLOAT_SUB:
                    case CPUI_FLOAT_NEG:
                    case CPUI_FLOAT_ABS:
                    case CPUI_FLOAT_SQRT:
                    case CPUI_FLOAT_INT2FLOAT:
                    case CPUI_FLOAT_FLOAT2FLOAT:
                    case CPUI_FLOAT_TRUNC:
                    case CPUI_FLOAT_CEIL:
                    case CPUI_FLOAT_FLOOR:
                    case CPUI_FLOAT_ROUND:
                        vn->getValueSet()->setFull();
                        rootNodes.push_back(vn->getValueSet());
                        break;
                    default:
                        for (int4 i = 0; i < op->numInput(); ++i)
                        {
                            Varnode* inVn = op->getIn(i);
                            if (inVn->isMark() || inVn->isAnnotation()) continue;
                            newValueSet(inVn, 0);
                            inVn->setMark();
                            worklist.push_back(inVn);
                        }
                        break;
                }
            }
            for (int4 i = 0; i < reads.size(); ++i)
            {
                PcodeOp* op = reads[i];
                for (int4 slot = 0; slot < op->numInput(); ++slot)
                {
                    Varnode* vn = op->getIn(slot);
                    if (vn->isMark())
                    {
                        readNodes[op->getSeqNum()].setPcodeOp(op, slot);
                        op->setMark();          // Mark read ops for equation generation stage
                        break;          // Only 1 read allowed
                    }
                }
            }
            generateConstraints(worklist, reads);
            for (int4 i = 0; i < reads.size(); ++i)
                reads[i]->clearMark();      // Clear marks on read ops

            establishTopologicalOrder();
            for (int4 i = 0; i < worklist.size(); ++i)
                worklist[i]->clearMark();
        }

        /// Get the current number of iterations
        public int4 getNumIterations() => numIterations;

        /// Iterate the ValueSet system until it stabilizes
        /// The ValueSets are recalculated in the established topological ordering, with looping
        /// at various levels until a fixed point is reached.
        /// \param max is the maximum number of iterations to allow before forcing termination
        /// \param widener is the Widening strategy to use to accelerate stabilization
        public void solve(int4 max, Widener widener)
        {
            maxIterations = max;
            numIterations = 0;
            for (list<ValueSet>::iterator iter = valueNodes.begin(); iter != valueNodes.end(); ++iter)
                (*iter).count = 0;

            vector<Partition*> componentStack;
            Partition* curComponent = (Partition*)0;
            ValueSet* curSet = orderPartition.startNode;

            while (curSet != (ValueSet*)0)
            {
                numIterations += 1;
                if (numIterations > maxIterations) break;   // Quit if max iterations exceeded
                if (curSet->partHead != (Partition*)0 && curSet->partHead != curComponent)
                {
                    componentStack.push_back(curSet->partHead);
                    curComponent = curSet->partHead;
                    curComponent->isDirty = false;
                    // Reset component counter upon entry
                    curComponent->startNode->count = widener.determineIterationReset(*curComponent->startNode);
                }
                if (curComponent != (Partition*)0)
                {
                    if (curSet->iterate(widener))
                        curComponent->isDirty = true;
                    if (curComponent->stopNode != curSet)
                    {
                        curSet = curSet->next;
                    }
                    else
                    {
                        for (; ; )
                        {
                            if (curComponent->isDirty)
                            {
                                curComponent->isDirty = false;
                                curSet = curComponent->startNode;
                                if (componentStack.size() > 1)
                                {   // Mark parent as dirty if we are restarting dirty child
                                    componentStack[componentStack.size() - 2]->isDirty = true;
                                }
                                break;
                            }

                            componentStack.pop_back();
                            if (componentStack.empty())
                            {
                                curComponent = (Partition*)0;
                                curSet = curSet->next;
                                break;
                            }
                            curComponent = componentStack.back();
                            if (curComponent->stopNode != curSet)
                            {
                                curSet = curSet->next;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    curSet->iterate(widener);
                    curSet = curSet->next;
                }
            }
            map<SeqNum, ValueSetRead>::iterator riter;
            for (riter = readNodes.begin(); riter != readNodes.end(); ++riter)
                (*riter).second.compute();              // Calculate any follow-on value sets
        }

        /// Start of all ValueSets in the system
        public IEnumerator<ValueSet> beginValueSets() => valueNodes.begin();

        /// End of all ValueSets in the system
        public IEnumerator<ValueSet> endValueSets() => valueNodes.end();

        /// Start of ValueSetReads
        public IEnumerator<KeyValuePair<SeqNum, ValueSetRead>> beginValueSetReads() => readNodes.begin();

        /// End of ValueSetReads
        public IEnumerator<KeyValuePair<SeqNum, ValueSetRead>> endValueSetReads() => readNodes.end();

        /// Get ValueSetRead by SeqNum
        public ValueSetRead getValueSetRead(SeqNum seq) => (*readNodes.find(seq)).second;

#if CPUI_DEBUG
        public void dumpValueSets(TextWriter s)
        {
          list<ValueSet>::const_iterator iter;
          for(iter=valueNodes.begin();iter!=valueNodes.end();++iter) {
            (*iter).printRaw(s);
            s << endl;
          }
          map<SeqNum,ValueSetRead>::const_iterator riter;
          for(riter=readNodes.begin();riter!=readNodes.end();++riter) {
            (*riter).second.printRaw(s);
            s << endl;
          }
        }
#endif
    }
}