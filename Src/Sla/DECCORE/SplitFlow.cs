using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for splitting up Varnodes that hold 2 logical variables
    ///
    /// Starting from a \e root Varnode provided to the constructor, \b this class looks for data-flow
    /// that consistently holds 2 logical values in a single Varnode. If doTrace() returns \b true,
    /// a consistent view has been created and invoking apply() will split all Varnodes  and PcodeOps
    /// involved in the data-flow into their logical pieces.
    internal class SplitFlow : TransformManager
    {
        /// Description of how to split Varnodes
        private LaneDescription laneDescription;
        /// Pending work list of Varnodes to push the split through
        private List<TransformVar> worklist;

        /// \brief Find or build the placeholder objects for a Varnode that needs to be split
        ///
        /// Mark the Varnode so it doesn't get revisited.
        /// Decide if the Varnode needs to go into the worklist.
        /// \param vn is the Varnode that needs to be split
        /// \return the array of placeholders describing the split or null
        private TransformVar setReplacement(Varnode vn)
        {
            TransformVar* res;
            if (vn.isMark())
            {       // Already seen before
                res = getSplit(vn, laneDescription);
                return res;
            }

            if (vn.isTypeLock() && vn.getType().getMetatype() != type_metatype.TYPE_PARTIALSTRUCT)
                return (TransformVar*)0;
            if (vn.isInput())
                return (TransformVar*)0;        // Right now we can't split inputs
            if (vn.isFree() && (!vn.isConstant()))
                return (TransformVar*)0;        // Abort

            res = newSplit(vn, laneDescription);    // Create new ReplaceVarnode and put it in map
            vn.setMark();
            if (!vn.isConstant())
                worklist.Add(res);

            return res;
        }

        /// \brief Split given op into its lanes.
        ///
        /// We assume op is a logical operation, or a COPY, or an INDIRECT. It must have an output.
        /// All inputs and output have their placeholders generated and added to the worklist
        /// if appropriate.
        /// \param op is the given op
        /// \param rvn is a known parameter of the op
        /// \param slot is the incoming slot of the known parameter (-1 means parameter is output)
        /// \return \b true if the op is successfully split
        private bool addOp(PcodeOp op, TransformVar rvn, int slot)
        {
            TransformVar* outvn;
            if (slot == -1)
                outvn = rvn;
            else
            {
                outvn = setReplacement(op.getOut());
                if (outvn == (TransformVar*)0)
                    return false;
            }

            if (outvn.getDef() != (TransformOp*)0)
                return true;    // Already traversed

            TransformOp* loOp = newOpReplace(op.numInput(), op.code(), op);
            TransformOp* hiOp = newOpReplace(op.numInput(), op.code(), op);
            int numParam = op.numInput();
            if (op.code() == OpCode.CPUI_INDIRECT)
            {
                opSetInput(loOp, newIop(op.getIn(1)), 1);
                opSetInput(hiOp, newIop(op.getIn(1)), 1);
                numParam = 1;
            }
            for (int i = 0; i < numParam; ++i)
            {
                TransformVar* invn;
                if (i == slot)
                    invn = rvn;
                else
                {
                    invn = setReplacement(op.getIn(i));
                    if (invn == (TransformVar*)0)
                        return false;
                }
                opSetInput(loOp, invn, i);      // Low piece with low op
                opSetInput(hiOp, invn + 1, i);      // High piece with high op
            }
            opSetOutput(loOp, outvn);
            opSetOutput(hiOp, outvn + 1);
            return true;
        }

        /// \brief Try to trace the pair of logical values, forward, through ops that read them
        ///
        /// Try to trace pieces of TransformVar pair forward, through reading ops, update worklist
        /// \param rvn is the TransformVar pair to trace, as an array
        /// \return \b true if logical pieces can be naturally traced, \b false otherwise
        private bool traceForward(TransformVar rvn)
        {
            Varnode* origvn = rvn.getOriginal();
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = origvn.beginDescend();
            enditer = origvn.endDescend();
            while (iter != enditer)
            {
                PcodeOp* op = *iter++;
                Varnode* outvn = op.getOut();
                if ((outvn != (Varnode)null) && (outvn.isMark()))
                    continue;
                switch (op.code())
                {
                    case OpCode.CPUI_COPY:
                    case OpCode.CPUI_MULTIEQUAL:
                    case OpCode.CPUI_INDIRECT:
                    case OpCode.CPUI_INT_AND:
                    case OpCode.CPUI_INT_OR:
                    case OpCode.CPUI_INT_XOR:
                        //  case OpCode.CPUI_INT_NEGATE:
                        if (!addOp(op, rvn, op.getSlot(origvn)))
                            return false;
                        break;
                    case OpCode.CPUI_SUBPIECE:
                        {
                            if (outvn.isPrecisLo() || outvn.isPrecisHi())
                                return false;       // Do not split if we know value comes from double precision pieces
                            ulong val = op.getIn(1).getOffset();
                            if ((val == 0) && (outvn.getSize() == laneDescription.getSize(0)))
                            {
                                TransformOp* rop = newPreexistingOp(1, OpCode.CPUI_COPY, op);  // Grabs the low piece
                                opSetInput(rop, rvn, 0);
                            }
                            else if ((val == laneDescription.getSize(0)) && (outvn.getSize() == laneDescription.getSize(1)))
                            {
                                TransformOp* rop = newPreexistingOp(1, OpCode.CPUI_COPY, op);  // Grabs the high piece
                                opSetInput(rop, rvn + 1, 0);
                            }
                            else
                                return false;
                            break;
                        }
                    case OpCode.CPUI_INT_LEFT:
                        {
                            Varnode* tmpvn = op.getIn(1);
                            if (!tmpvn.isConstant())
                                return false;
                            ulong val = tmpvn.getOffset();
                            if (val < laneDescription.getSize(1) * 8)
                                return false;           // Must obliterate all high bits
                            TransformOp* rop = newPreexistingOp(2, OpCode.CPUI_INT_LEFT, op);      // Keep original shift
                            TransformOp* zextrop = newOp(1, OpCode.CPUI_INT_ZEXT, rop);
                            opSetInput(zextrop, rvn, 0);        // Input is just the low piece
                            opSetOutput(zextrop, newUnique(laneDescription.getWholeSize()));
                            opSetInput(rop, zextrop.getOut(), 0);
                            opSetInput(rop, newConstant(op.getIn(1).getSize(), 0, op.getIn(1).getOffset()), 1); // Original shift amount
                            break;
                        }
                    case OpCode.CPUI_INT_SRIGHT:
                    case OpCode.CPUI_INT_RIGHT:
                        {
                            Varnode* tmpvn = op.getIn(1);
                            if (!tmpvn.isConstant())
                                return false;
                            ulong val = tmpvn.getOffset();
                            if (val < laneDescription.getSize(0) * 8)
                                return false;
                            OpCode extOpCode = (op.code() == OpCode.CPUI_INT_RIGHT) ? OpCode.CPUI_INT_ZEXT : OpCode.CPUI_INT_SEXT;
                            if (val == laneDescription.getSize(0) * 8)
                            {   // Shift of exactly loSize bytes
                                TransformOp* rop = newPreexistingOp(1, extOpCode, op);
                                opSetInput(rop, rvn + 1, 0);    // Input is the high piece
                            }
                            else
                            {
                                ulong remainShift = val - laneDescription.getSize(0) * 8;
                                TransformOp* rop = newPreexistingOp(2, op.code(), op);
                                TransformOp* extrop = newOp(1, extOpCode, rop);
                                opSetInput(extrop, rvn + 1, 0); // Input is the high piece
                                opSetOutput(extrop, newUnique(laneDescription.getWholeSize()));
                                opSetInput(rop, extrop.getOut(), 0);
                                opSetInput(rop, newConstant(op.getIn(1).getSize(), 0, remainShift), 1);   // Shift any remaining bits
                            }
                            break;
                        }
                    default:
                        return false;
                }
            }
            return true;
        }

        /// \brief Try to trace the pair of logical values, backward, through the defining op
        ///
        /// Create part of transform related to the defining op, and update the worklist as necessary.
        /// \param rvn is the logical value to examine
        /// \return \b false if the trace is not possible
        private bool traceBackward(TransformVar rvn)
        {
            PcodeOp* op = rvn.getOriginal().getDef();
            if (op == (PcodeOp)null) return true; // If vn is input

            switch (op.code())
            {
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_MULTIEQUAL:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INDIRECT:
                    //  case OpCode.CPUI_INT_NEGATE:
                    if (!addOp(op, rvn, -1))
                        return false;
                    break;
                case OpCode.CPUI_PIECE:
                    {
                        if (op.getIn(0).getSize() != laneDescription.getSize(1))
                            return false;
                        if (op.getIn(1).getSize() != laneDescription.getSize(0))
                            return false;
                        TransformOp* loOp = newOpReplace(1, OpCode.CPUI_COPY, op);
                        TransformOp* hiOp = newOpReplace(1, OpCode.CPUI_COPY, op);
                        opSetInput(loOp, getPreexistingVarnode(op.getIn(1)), 0);
                        opSetOutput(loOp, rvn); // Least sig . low
                        opSetInput(hiOp, getPreexistingVarnode(op.getIn(0)), 0);
                        opSetOutput(hiOp, rvn + 1); // Most sig . high
                        break;
                    }
                case OpCode.CPUI_INT_ZEXT:
                    {
                        if (op.getIn(0).getSize() != laneDescription.getSize(0))
                            return false;
                        if (op.getOut().getSize() != laneDescription.getWholeSize())
                            return false;
                        TransformOp* loOp = newOpReplace(1, OpCode.CPUI_COPY, op);
                        TransformOp* hiOp = newOpReplace(1, OpCode.CPUI_COPY, op);
                        opSetInput(loOp, getPreexistingVarnode(op.getIn(0)), 0);
                        opSetOutput(loOp, rvn); // ZEXT input . low
                        opSetInput(hiOp, newConstant(laneDescription.getSize(1), 0, 0), 0);
                        opSetOutput(hiOp, rvn + 1); // zero . high
                        break;
                    }
                case OpCode.CPUI_INT_LEFT:
                    {
                        Varnode* cvn = op.getIn(1);
                        if (!cvn.isConstant()) return false;
                        if (cvn.getOffset() != laneDescription.getSize(0) * 8) return false;
                        Varnode* invn = op.getIn(0);
                        if (!invn.isWritten()) return false;
                        PcodeOp* zextOp = invn.getDef();
                        if (zextOp.code() != OpCode.CPUI_INT_ZEXT) return false;
                        invn = zextOp.getIn(0);
                        if (invn.getSize() != laneDescription.getSize(1)) return false;
                        if (invn.isFree()) return false;
                        TransformOp* loOp = newOpReplace(1, OpCode.CPUI_COPY, op);
                        TransformOp* hiOp = newOpReplace(1, OpCode.CPUI_COPY, op);
                        opSetInput(loOp, newConstant(laneDescription.getSize(0), 0, 0), 0);
                        opSetOutput(loOp, rvn); // zero . low
                        opSetInput(hiOp, getPreexistingVarnode(invn), 0);
                        opSetOutput(hiOp, rvn + 1); // invn . high
                        break;
                    }
                //  case OpCode.CPUI_LOAD:		// We could split into two different loads
                default:
                    return false;
            }
            return true;
        }

        /// Process the next logical value on the worklist
        /// \return \b true if the logical split was successfully pushed through its local operators
        private bool processNextWork()
        {
            TransformVar* rvn = worklist.GetLastItem();

            worklist.RemoveLastItem();

            if (!traceBackward(rvn)) return false;
            return traceForward(rvn);
        }

        public SplitFlow(Funcdata f, Varnode root, int lowSize)
            : base(f)

        {
            laneDescription = new LaneDescription(root.getSize(), lowSize, root.getSize() - lowSize);
            setReplacement(root);
        }

        /// Trace split through data-flow, constructing transform
        /// Push the logical split around, setting up the explicit transforms as we go.
        /// If at any point, the split cannot be naturally pushed, return \b false.
        /// \return \b true if a full transform has been constructed that can perform the split
        public bool doTrace()
        {
            if (worklist.empty())
                return false;       // Nothing to do
            bool retval = true;
            while (!worklist.empty())
            {   // Process the worklist until its done
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
