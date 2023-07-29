using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Sla.DECCORE
{
    /// \brief Class for splitting data-flow on \e laned registers
    ///
    /// From a root Varnode and a description of its \e lanes, trace data-flow as far as
    /// possible through the function, propagating each lane, using the doTrace() method.  Then
    /// using the apply() method, data-flow can be split, making each lane in every traced
    /// register into an explicit Varnode
    internal class LaneDivide : TransformManager
    {
        /// \brief Description of a large Varnode that needs to be traced (in the worklist)
        private class WorkNode
        {
            // friend class LaneDivide;
            /// Lane placeholders for underyling Varnode
            internal TransformVar lanes;
            /// Number of lanes in the particular Varnode
            internal int numLanes;
            /// Number of lanes to skip in the global description
            internal int skipLanes;
        }

        /// Global description of lanes that need to be split
        private LaneDescription description;
        /// List of Varnodes still left to trace
        private List<WorkNode> workList;
        /// \b true if we allow lanes to be cast (via SUBPIECE) to a smaller integer size
        private bool allowSubpieceTerminator;

        /// \brief Find or build the placeholder objects for a Varnode that needs to be split into lanes
        ///
        /// The Varnode is split based on the given subset of the lane description.
        /// Constants can be split. Decide if the Varnode needs to go into the work list.
        /// If the Varnode cannot be acceptably split, return null.
        /// \param vn is the Varnode that needs to be split
        /// \param numLanes is the number of lanes in the subset
        /// \param skipLanes is the start (least significant) lane in the subset
        /// \return the array of placeholders describing the split or null
        private TransformVar setReplacement(Varnode vn, int numLanes, int skipLanes)
        {
            if (vn.isMark())       // Already seen before
                return getSplit(vn, description, numLanes, skipLanes);

            if (vn.isConstant())
            {
                return newSplit(vn, description, numLanes, skipLanes);
            }

            // Allow free varnodes to be split
            //  if (vn.isFree())
            //    return (TransformVar *)0;

            if (vn.isTypeLock() && vn.getType().getMetatype() != TYPE_PARTIALSTRUCT)
            {
                return (TransformVar*)0;
            }

            vn.setMark();
            TransformVar* res = newSplit(vn, description, numLanes, skipLanes);
            if (!vn.isFree())
            {
                workList.emplace_back();
                workList.back().lanes = res;
                workList.back().numLanes = numLanes;
                workList.back().skipLanes = skipLanes;
            }
            return res;
        }

        /// \brief Build unary op placeholders with the same opcode across a set of lanes
        ///
        /// We assume the input and output placeholder variables have already been collected
        /// \param opc is the desired opcode for the new op placeholders
        /// \param op is the PcodeOp getting replaced
        /// \param inVars is the array of input variables, 1 for each unary op
        /// \param outVars is the array of output variables, 1 for each unary op
        /// \param numLanes is the number of unary ops to create
        private void buildUnaryOp(OpCode opc, PcodeOp op, TransformVar inVars, TransformVar outVars,
            int numLanes)
        {
            for (int i = 0; i < numLanes; ++i)
            {
                TransformOp* rop = newOpReplace(1, opc, op);
                opSetOutput(rop, outVars + i);
                opSetInput(rop, inVars + i, 0);
            }
        }

        /// \brief Build binary op placeholders with the same opcode across a set of lanes
        ///
        /// We assume the input and output placeholder variables have already been collected
        /// \param opc is the desired opcode for the new op placeholders
        /// \param op is the PcodeOp getting replaced
        /// \param in0Vars is the array of input[0] variables, 1 for each binary op
        /// \param in1Vars is the array of input[1] variables, 1 for each binar op
        /// \param outVars is the array of output variables, 1 for each binary op
        /// \param numLanes is the number of binary ops to create
        private void buildBinaryOp(OpCode opc, PcodeOp op, TransformVar in0Vars, TransformVar in1Vars,
            TransformVar outVars, int numLanes)
        {
            for (int i = 0; i < numLanes; ++i)
            {
                TransformOp* rop = newOpReplace(2, opc, op);
                opSetOutput(rop, outVars + i);
                opSetInput(rop, in0Vars + i, 0);
                opSetInput(rop, in1Vars + i, 1);
            }
        }

        /// \brief Convert a CPUI_PIECE operation into copies between placeholders, given the output lanes
        ///
        /// Model the given CPUI_PIECE either as either copies from preexisting Varnodes into the
        /// output lanes, or as copies from placeholder variables into the output lanes.  Return \b false
        /// if the operation cannot be modeled as natural copies between lanes.
        /// \param op is the original CPUI_PIECE PcodeOp
        /// \param outVars is the placeholder variables making up the lanes of the output
        /// \param numLanes is the number of lanes in the output
        /// \param skipLanes is the index of the least significant output lane within the global description
        /// \return \b true if the CPUI_PIECE was modeled as natural lane copies
        private bool buildPiece(PcodeOp op, TransformVar outVars, int numLanes, int skipLanes)
        {
            int highLanes, highSkip;
            int lowLanes, lowSkip;
            Varnode* highVn = op.getIn(0);
            Varnode* lowVn = op.getIn(1);

            if (!description.restriction(numLanes, skipLanes, lowVn.getSize(), highVn.getSize(), highLanes, highSkip))
                return false;
            if (!description.restriction(numLanes, skipLanes, 0, lowVn.getSize(), lowLanes, lowSkip))
                return false;
            if (highLanes == 1)
            {
                TransformVar* highRvn = getPreexistingVarnode(highVn);
                TransformOp* rop = newOpReplace(1, CPUI_COPY, op);
                opSetInput(rop, highRvn, 0);
                opSetOutput(rop, outVars + (numLanes - 1));
            }
            else
            {   // Multi-lane high
                TransformVar* highRvn = setReplacement(highVn, highLanes, highSkip);
                if (highRvn == (TransformVar*)0) return false;
                int outHighStart = numLanes - highLanes;
                for (int i = 0; i < highLanes; ++i)
                {
                    TransformOp* rop = newOpReplace(1, CPUI_COPY, op);
                    opSetInput(rop, highRvn + i, 0);
                    opSetOutput(rop, outVars + (outHighStart + i));
                }
            }
            if (lowLanes == 1)
            {
                TransformVar* lowRvn = getPreexistingVarnode(lowVn);
                TransformOp* rop = newOpReplace(1, CPUI_COPY, op);
                opSetInput(rop, lowRvn, 0);
                opSetOutput(rop, outVars);
            }
            else
            {   // Multi-lane low
                TransformVar* lowRvn = setReplacement(lowVn, lowLanes, lowSkip);
                if (lowRvn == (TransformVar*)0) return false;
                for (int i = 0; i < lowLanes; ++i)
                {
                    TransformOp* rop = newOpReplace(1, CPUI_COPY, op);
                    opSetInput(rop, lowRvn + i, 0);
                    opSetOutput(rop, outVars + i);
                }
            }
            return true;
        }

        /// \brief Split a given CPUI_MULTIEQUAL operation into placeholders given the output lanes
        ///
        /// Model the single given CPUI_MULTIEQUAL as a sequence of smaller MULTIEQUALs on
        /// each individual lane. Return \b false if the operation cannot be modeled as naturally.
        /// \param op is the original CPUI_MULTIEQUAL PcodeOp
        /// \param outVars is the placeholder variables making up the lanes of the output
        /// \param numLanes is the number of lanes in the output
        /// \param skipLanes is the index of the least significant output lane within the global description
        /// \return \b true if the operation was fully modeled
        private bool buildMultiequal(PcodeOp op, TransformVar outVars, int numLanes, int skipLanes)
        {
            List<TransformVar*> inVarSets;
            int numInput = op.numInput();
            for (int i = 0; i < numInput; ++i)
            {
                TransformVar* inVn = setReplacement(op.getIn(i), numLanes, skipLanes);
                if (inVn == (TransformVar*)0) return false;
                inVarSets.push_back(inVn);
            }
            for (int i = 0; i < numLanes; ++i)
            {
                TransformOp* rop = newOpReplace(numInput, CPUI_MULTIEQUAL, op);
                opSetOutput(rop, outVars + i);
                for (int j = 0; j < numInput; ++j)
                    opSetInput(rop, inVarSets[j] + i, j);
            }
            return true;
        }

        /// \brief Split a given CPUI_STORE operation into a sequence of STOREs of individual lanes
        ///
        /// A new pointer is constructed for each individual lane into a temporary, then a
        /// STORE is created using the pointer that stores an individual lane.
        /// \param op is the given CPUI_STORE PcodeOp
        /// \param numLanes is the number of lanes the STORE is split into
        /// \param skipLanes is the starting lane (within the global description) of the value being stored
        /// \return \b true if the CPUI_STORE was successfully modeled on lanes
        private bool buildStore(PcodeOp op, int numLanes, int skipLanes)
        {
            TransformVar* inVars = setReplacement(op.getIn(2), numLanes, skipLanes);
            if (inVars == (TransformVar*)0) return false;
            ulong spaceConst = op.getIn(0).getOffset();
            int spaceConstSize = op.getIn(0).getSize();
            AddrSpace* spc = op.getIn(0).getSpaceFromConst(); // Address space being stored to
            Varnode* origPtr = op.getIn(1);
            if (origPtr.isFree())
            {
                if (!origPtr.isConstant()) return false;
            }
            TransformVar* basePtr = getPreexistingVarnode(origPtr);
            int ptrSize = origPtr.getSize();
            Varnode* valueVn = op.getIn(2);
            for (int i = 0; i < numLanes; ++i)
            {
                TransformOp* ropStore = newOpReplace(3, CPUI_STORE, op);
                int bytePos = description.getPosition(skipLanes + i);
                int sz = description.getSize(skipLanes + i);
                if (spc.isBigEndian())
                    bytePos = valueVn.getSize() - (bytePos + sz);  // Convert position to address order

                // Construct the pointer
                TransformVar* ptrVn;
                if (bytePos == 0)
                    ptrVn = basePtr;
                else
                {
                    ptrVn = newUnique(ptrSize);
                    TransformOp* addOp = newOp(2, CPUI_INT_ADD, ropStore);
                    opSetOutput(addOp, ptrVn);
                    opSetInput(addOp, basePtr, 0);
                    opSetInput(addOp, newConstant(ptrSize, 0, bytePos), 1);
                }

                opSetInput(ropStore, newConstant(spaceConstSize, 0, spaceConst), 0);
                opSetInput(ropStore, ptrVn, 1);
                opSetInput(ropStore, inVars + i, 2);
            }
            return true;
        }

        /// \brief Split a given CPUI_LOAD operation into a sequence of LOADs of individual lanes
        ///
        /// A new pointer is constructed for each individual lane into a temporary, then a
        /// LOAD is created using the pointer that loads an individual lane.
        /// \param op is the given CPUI_LOAD PcodeOp
        /// \param outVars is the output placeholders for the LOAD
        /// \param numLanes is the number of lanes the LOAD is split into
        /// \param skipLanes is the starting lane (within the global description) of the value being loaded
        /// \return \b true if the CPUI_LOAD was successfully modeled on lanes
        private bool buildLoad(PcodeOp op, TransformVar outVars, int numLanes, int skipLanes)
        {
            ulong spaceConst = op.getIn(0).getOffset();
            int spaceConstSize = op.getIn(0).getSize();
            AddrSpace* spc = op.getIn(0).getSpaceFromConst(); // Address space being stored to
            Varnode* origPtr = op.getIn(1);
            if (origPtr.isFree())
            {
                if (!origPtr.isConstant()) return false;
            }
            TransformVar* basePtr = getPreexistingVarnode(origPtr);
            int ptrSize = origPtr.getSize();
            int outSize = op.getOut().getSize();
            for (int i = 0; i < numLanes; ++i)
            {
                TransformOp* ropLoad = newOpReplace(2, CPUI_LOAD, op);
                int bytePos = description.getPosition(skipLanes + i);
                int sz = description.getSize(skipLanes + i);
                if (spc.isBigEndian())
                    bytePos = outSize - (bytePos + sz); // Convert position to address order

                // Construct the pointer
                TransformVar* ptrVn;
                if (bytePos == 0)
                    ptrVn = basePtr;
                else
                {
                    ptrVn = newUnique(ptrSize);
                    TransformOp* addOp = newOp(2, CPUI_INT_ADD, ropLoad);
                    opSetOutput(addOp, ptrVn);
                    opSetInput(addOp, basePtr, 0);
                    opSetInput(addOp, newConstant(ptrSize, 0, bytePos), 1);
                }

                opSetInput(ropLoad, newConstant(spaceConstSize, 0, spaceConst), 0);
                opSetInput(ropLoad, ptrVn, 1);
                opSetOutput(ropLoad, outVars + i);
            }
            return true;
        }

        /// \brief Check that a CPUI_INT_RIGHT respects the lanes then generate lane placeholders
        ///
        /// For the given lane scheme, check that the RIGHT shift is copying whole lanes to each other.
        /// If so, generate the placeholder COPYs that model the shift.
        /// \param op is the given CPUI_INT_RIGHT PcodeOp
        /// \param outVars is the output placeholders for the RIGHT shift
        /// \param numLanes is the number of lanes the shift is split into
        /// \param skipLanes is the starting lane (within the global description) of the value being loaded
        /// \return \b true if the CPUI_INT_RIGHT was successfully modeled on lanes
        private bool buildRightShift(PcodeOp op, TransformVar outVars, int numLanes, int skipLanes)
        {
            if (!op.getIn(1).isConstant()) return false;
            int shiftSize = (int)op.getIn(1).getOffset();
            if ((shiftSize & 7) != 0) return false;     // Not a multiple of 8
            shiftSize /= 8;
            int startPos = shiftSize + description.getPosition(skipLanes);
            int startLane = description.getBoundary(startPos);
            if (startLane < 0) return false;        // Shift does not end on a lane boundary
            int srcLane = startLane;
            int destLane = skipLanes;
            while (srcLane - skipLanes < numLanes)
            {
                if (description.getSize(srcLane) != description.getSize(destLane)) return false;
                srcLane += 1;
                destLane += 1;
            }
            TransformVar* inVars = setReplacement(op.getIn(0), numLanes, skipLanes);
            if (inVars == (TransformVar*)0) return false;
            buildUnaryOp(CPUI_COPY, op, inVars + (startLane - skipLanes), outVars, numLanes - (startLane - skipLanes));
            for (int zeroLane = numLanes - (startLane - skipLanes); zeroLane < numLanes; ++zeroLane)
            {
                TransformOp* rop = newOpReplace(1, CPUI_COPY, op);
                opSetOutput(rop, outVars + zeroLane);
                opSetInput(rop, newConstant(description.getSize(zeroLane), 0, 0), 0);
            }
            return true;
        }

        /// \brief Push the logical lanes forward through any PcodeOp reading the given variable
        ///
        /// Determine if the logical lanes can be pushed forward naturally, and create placeholder
        /// variables and ops representing the logical data-flow.  Update the worklist with any
        /// new Varnodes that the lanes get pushed into.
        /// \param rvn is the placeholder variable to push forward from
        /// \param numLanes is the number of lanes represented by the placeholder variable
        /// \param skipLanes is the index of the starting lane within the global description of the placeholder variable
        /// \return \b true if the lanes can be naturally pushed forward
        private bool traceForward(TransformVar rvn, int numLanes, int skipLanes)
        {
            Varnode* origvn = rvn.getOriginal();
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = origvn.beginDescend();
            enditer = origvn.endDescend();
            while (iter != enditer)
            {
                PcodeOp* op = *iter++;
                Varnode* outvn = op.getOut();
                if ((outvn != (Varnode*)0) && (outvn.isMark()))
                    continue;
                switch (op.code())
                {
                    case CPUI_SUBPIECE:
                        {
                            int bytePos = (int)op.getIn(1).getOffset();
                            int outLanes, outSkip;
                            if (!description.restriction(numLanes, skipLanes, bytePos, outvn.getSize(), outLanes, outSkip))
                            {
                                if (allowSubpieceTerminator)
                                {
                                    int laneIndex = description.getBoundary(bytePos);
                                    if (laneIndex < 0 || laneIndex >= description.getNumLanes())    // Does piece start on lane boundary?
                                        return false;
                                    if (description.getSize(laneIndex) <= outvn.getSize())     // Is the piece smaller than a lane?
                                        return false;
                                    // Treat SUBPIECE as terminating
                                    TransformOp* rop = newPreexistingOp(2, CPUI_SUBPIECE, op);
                                    opSetInput(rop, rvn + (laneIndex - skipLanes), 0);
                                    opSetInput(rop, newConstant(4, 0, 0), 1);
                                    break;
                                }
                                return false;
                            }
                            if (outLanes == 1)
                            {
                                TransformOp* rop = newPreexistingOp(1, CPUI_COPY, op);
                                opSetInput(rop, rvn + (outSkip - skipLanes), 0);
                            }
                            else
                            {
                                TransformVar* outRvn = setReplacement(outvn, outLanes, outSkip);
                                if (outRvn == (TransformVar*)0) return false;
                                // Don't create the placeholder ops, let traceBackward make them
                            }
                            break;
                        }
                    case CPUI_PIECE:
                        {
                            int outLanes, outSkip;
                            int bytePos = (op.getIn(0) == origvn) ? op.getIn(1).getSize() : 0;
                            if (!description.extension(numLanes, skipLanes, bytePos, outvn.getSize(), outLanes, outSkip))
                                return false;
                            TransformVar* outRvn = setReplacement(outvn, outLanes, outSkip);
                            if (outRvn == (TransformVar*)0) return false;
                            // Don't create the placeholder ops, let traceBackward make them
                            break;
                        }
                    case CPUI_COPY:
                    case CPUI_INT_NEGATE:
                    case CPUI_INT_AND:
                    case CPUI_INT_OR:
                    case CPUI_INT_XOR:
                    case CPUI_MULTIEQUAL:
                        {
                            TransformVar* outRvn = setReplacement(outvn, numLanes, skipLanes);
                            if (outRvn == (TransformVar*)0) return false;
                            // Don't create the placeholder ops, let traceBackward make them
                            break;
                        }
                    case CPUI_INT_RIGHT:
                        {
                            if (!op.getIn(1).isConstant()) return false;  // Trace must come through op.getIn(0)
                            TransformVar* outRvn = setReplacement(outvn, numLanes, skipLanes);
                            if (outRvn == (TransformVar*)0) return false;
                            // Don't create the placeholder ops, let traceBackward make them
                            break;
                        }
                    case CPUI_STORE:
                        if (op.getIn(2) != origvn) return false;   // Can only propagate through value being stored
                        if (!buildStore(op, numLanes, skipLanes))
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        /// \brief Pull the logical lanes back through the defining PcodeOp of the given variable
        ///
        /// Determine if the logical lanes can be pulled back naturally, and create placeholder
        /// variables and ops representing the logical data-flow.  Update the worklist with any
        /// new Varnodes that the lanes get pulled back into.
        /// \param rvn is the placeholder variable to pull back
        /// \param numLanes is the number of lanes represented by the placeholder variable
        /// \param skipLanes is the index of the starting lane within the global description of the placeholder variable
        /// \return \b true if the lanes can be naturally pulled back
        private bool traceBackward(TransformVar rvn, int numLanes, int skipLanes)
        {
            PcodeOp* op = rvn.getOriginal().getDef();
            if (op == (PcodeOp*)0) return true; // If vn is input

            switch (op.code())
            {
                case CPUI_INT_NEGATE:
                case CPUI_COPY:
                    {
                        TransformVar* inVars = setReplacement(op.getIn(0), numLanes, skipLanes);
                        if (inVars == (TransformVar*)0) return false;
                        buildUnaryOp(op.code(), op, inVars, rvn, numLanes);
                        break;
                    }
                case CPUI_INT_AND:
                case CPUI_INT_OR:
                case CPUI_INT_XOR:
                    {
                        TransformVar* in0Vars = setReplacement(op.getIn(0), numLanes, skipLanes);
                        if (in0Vars == (TransformVar*)0) return false;
                        TransformVar* in1Vars = setReplacement(op.getIn(1), numLanes, skipLanes);
                        if (in1Vars == (TransformVar*)0) return false;
                        buildBinaryOp(op.code(), op, in0Vars, in1Vars, rvn, numLanes);
                        break;
                    }
                case CPUI_MULTIEQUAL:
                    if (!buildMultiequal(op, rvn, numLanes, skipLanes))
                        return false;
                    break;
                case CPUI_SUBPIECE:
                    {
                        Varnode* inVn = op.getIn(0);
                        int bytePos = (int)op.getIn(1).getOffset();
                        int inLanes, inSkip;
                        if (!description.extension(numLanes, skipLanes, bytePos, inVn.getSize(), inLanes, inSkip))
                            return false;
                        TransformVar* inVars = setReplacement(inVn, inLanes, inSkip);
                        if (inVars == (TransformVar*)0) return false;
                        buildUnaryOp(CPUI_COPY, op, inVars + (skipLanes - inSkip), rvn, numLanes);
                        break;
                    }
                case CPUI_PIECE:
                    if (!buildPiece(op, rvn, numLanes, skipLanes))
                        return false;
                    break;
                case CPUI_LOAD:
                    if (!buildLoad(op, rvn, numLanes, skipLanes))
                        return false;
                    break;
                case CPUI_INT_RIGHT:
                    if (!buildRightShift(op, rvn, numLanes, skipLanes))
                        return false;
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// Process the next Varnode on the work list
        /// \return \b true if the lane split for the top Varnode on the work list is propagated through local operators
        private bool processNextWork()
        {
            TransformVar* rvn = workList.back().lanes;
            int numLanes = workList.back().numLanes;
            int skipLanes = workList.back().skipLanes;

            workList.pop_back();

            if (!traceBackward(rvn, numLanes, skipLanes)) return false;
            return traceForward(rvn, numLanes, skipLanes);
        }

        /// \param f is the function being transformed
        /// \param root is the root Varnode to start tracing lanes from
        /// \param desc is a description of the lanes on the root Varnode
        /// \param allowDowncast is \b true if we all SUBPIECE to be treated as terminating
        public LaneDivide(Funcdata f, Varnode root, LaneDescription desc,bool allowDowncast)
            : base(f)
        {
            description = desc;
            allowSubpieceTerminator = allowDowncast;
            setReplacement(root, desc.getNumLanes(), 0);
        }

        /// Trace lanes as far as possible from the root Varnode
        /// Push the lanes around from the root, setting up the explicit transforms as we go.
        /// If at any point, the lanes cannot be naturally pushed, return \b false.
        /// \return \b true if a full transform has been constructed that can split into explicit lanes
        public bool doTrace()
        {
            if (workList.empty())
                return false;       // Nothing to do
            bool retval = true;
            while (!workList.empty())
            {   // Process the work list until its done
                if (!processNextWork())
                {
                    retval = false;
                    break;
                }
            }

            clearVarnodeMarks();
            if (!retval) return false;
            return true;
        }
    }
}
