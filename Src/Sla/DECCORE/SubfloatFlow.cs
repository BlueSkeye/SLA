using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for tracing changes of precision in floating point variables
    ///
    /// It follows the flow of a logical lower precision value stored in higher precision locations
    /// and then rewrites the data-flow in terms of the lower precision, eliminating the
    /// precision conversions.
    internal class SubfloatFlow : TransformManager
    {
        /// Number of bytes of precision in the logical flow
        private int4 precision;
        /// Number of terminating nodes reachable via the root
        private int4 terminatorCount;
        /// The floating-point format of the logical value
        private FloatFormat format;
        /// Current list of placeholders that still need to be traced
        private List<TransformVar> worklist;

        /// \brief Create and return a placeholder associated with the given Varnode
        ///
        /// Add the placeholder to the worklist if it hasn't been visited before
        /// \param vn is the given Varnode
        /// \return the placeholder or null if the Varnode is not suitable for replacement
        private TransformVar setReplacement(Varnode vn)
        {
            if (vn->isMark())       // Already seen before
                return getPiece(vn, precision * 8, 0);

            if (vn->isConstant())
            {
                FloatFormat form2 = getFunction()->getArch()->translate->getFloatFormat(vn->getSize());
                if (form2 == (FloatFormat*)0)
                    return (TransformVar*)0;  // Unsupported constant format
                                // Return the converted form of the constant
                return newConstant(precision, 0, format->convertEncoding(vn->getOffset(), form2));
            }

            if (vn->isFree())
                return (TransformVar*)0; // Abort

            if (vn->isAddrForce() && (vn->getSize() != precision))
                return (TransformVar*)0;

            if (vn->isTypeLock() && vn->getType()->getMetatype() != TYPE_PARTIALSTRUCT)
            {
                int4 sz = vn->getType()->getSize();
                if (sz != precision)
                    return (TransformVar*)0;
            }

            if (vn->isInput())
            {       // Must be careful with inputs
                if (vn->getSize() != precision) return (TransformVar*)0;
            }

            vn->setMark();
            TransformVar* res;
            // Check if vn already represents the logical variable being traced
            if (vn->getSize() == precision)
                res = newPreexistingVarnode(vn);
            else
            {
                res = newPiece(vn, precision * 8, 0);
                worklist.push_back(res);
            }
            return res;
        }

        /// \brief Try to trace logical variable through descendant Varnodes
        ///
        /// Given a Varnode placeholder, look at all descendent PcodeOps and create
        /// placeholders for the op and its output Varnode.  If appropriate add the
        /// output placeholder to the worklist.
        /// \param rvn is the given Varnode placeholder
        /// \return \b true if tracing the logical variable forward was possible
        private bool traceForward(TransformVar rvn)
        {
            list<PcodeOp*>::const_iterator iter, enditer;
            Varnode* vn = rvn->getOriginal();
            iter = vn->beginDescend();
            enditer = vn->endDescend();
            while (iter != enditer)
            {
                PcodeOp* op = *iter++;
                Varnode* outvn = op->getOut();
                if ((outvn != (Varnode*)0) && (outvn->isMark()))
                    continue;
                switch (op->code())
                {
                    case CPUI_COPY:
                    case CPUI_FLOAT_CEIL:
                    case CPUI_FLOAT_FLOOR:
                    case CPUI_FLOAT_ROUND:
                    case CPUI_FLOAT_NEG:
                    case CPUI_FLOAT_ABS:
                    case CPUI_FLOAT_SQRT:
                    case CPUI_FLOAT_ADD:
                    case CPUI_FLOAT_SUB:
                    case CPUI_FLOAT_MULT:
                    case CPUI_FLOAT_DIV:
                    case CPUI_MULTIEQUAL:
                        {
                            TransformOp* rop = newOpReplace(op->numInput(), op->code(), op);
                            TransformVar* outrvn = setReplacement(outvn);
                            if (outrvn == (TransformVar*)0) return false;
                            opSetInput(rop, rvn, op->getSlot(vn));
                            opSetOutput(rop, outrvn);
                            break;
                        }
                    case CPUI_FLOAT_FLOAT2FLOAT:
                        {
                            if (outvn->getSize() < precision)
                                return false;
                            TransformOp* rop = newPreexistingOp(1, (outvn->getSize() == precision) ? CPUI_COPY : CPUI_FLOAT_FLOAT2FLOAT, op);
                            opSetInput(rop, rvn, 0);
                            terminatorCount += 1;
                            break;
                        }
                    case CPUI_FLOAT_EQUAL:
                    case CPUI_FLOAT_NOTEQUAL:
                    case CPUI_FLOAT_LESS:
                    case CPUI_FLOAT_LESSEQUAL:
                        {
                            int4 slot = op->getSlot(vn);
                            TransformVar* rvn2 = setReplacement(op->getIn(1 - slot));
                            if (rvn2 == (TransformVar*)0) return false;
                            if (rvn == rvn2)
                            {
                                list<PcodeOp*>::const_iterator ourIter = iter;
                                --ourIter;  // Back up one to our original iterator
                                slot = op->getRepeatSlot(vn, slot, ourIter);
                            }
                            if (preexistingGuard(slot, rvn2))
                            {
                                TransformOp* rop = newPreexistingOp(2, op->code(), op);
                                opSetInput(rop, rvn, 0);
                                opSetInput(rop, rvn2, 1);
                                terminatorCount += 1;
                            }
                            break;
                        }
                    case CPUI_FLOAT_TRUNC:
                    case CPUI_FLOAT_NAN:
                        {
                            TransformOp* rop = newPreexistingOp(1, op->code(), op);
                            opSetInput(rop, rvn, 0);
                            terminatorCount += 1;
                            break;
                        }
                    default:
                        return false;
                }
            }
            return true;
        }

        /// \brief Trace a logical value backward through defining op one level
        ///
        /// Given an existing variable placeholder look at the op defining it and
        /// define placeholder variables for all its inputs.  Put the new placeholders
        /// onto the worklist if appropriate.
        /// \param rvn is the given variable placeholder
        /// \return \b true if the logical value can be traced properly
        private bool traceBackward(TransformVar rvn)
        {
            PcodeOp* op = rvn->getOriginal()->getDef();
            if (op == (PcodeOp*)0) return true; // If vn is input

            switch (op->code())
            {
                case CPUI_COPY:
                case CPUI_FLOAT_CEIL:
                case CPUI_FLOAT_FLOOR:
                case CPUI_FLOAT_ROUND:
                case CPUI_FLOAT_NEG:
                case CPUI_FLOAT_ABS:
                case CPUI_FLOAT_SQRT:
                case CPUI_FLOAT_ADD:
                case CPUI_FLOAT_SUB:
                case CPUI_FLOAT_MULT:
                case CPUI_FLOAT_DIV:
                case CPUI_MULTIEQUAL:
                    {
                        TransformOp* rop = rvn->getDef();
                        if (rop == (TransformOp*)0)
                        {
                            rop = newOpReplace(op->numInput(), op->code(), op);
                            opSetOutput(rop, rvn);
                        }
                        for (int4 i = 0; i < op->numInput(); ++i)
                        {
                            TransformVar* newvar = rop->getIn(i);
                            if (newvar == (TransformVar*)0)
                            {
                                newvar = setReplacement(op->getIn(i));
                                if (newvar == (TransformVar*)0)
                                    return false;
                                opSetInput(rop, newvar, i);
                            }
                        }
                        return true;
                    }
                case CPUI_FLOAT_INT2FLOAT:
                    {
                        Varnode* vn = op->getIn(0);
                        if (!vn->isConstant() && vn->isFree())
                            return false;
                        TransformOp* rop = newOpReplace(1, CPUI_FLOAT_INT2FLOAT, op);
                        opSetOutput(rop, rvn);
                        TransformVar* newvar = getPreexistingVarnode(vn);
                        opSetInput(rop, newvar, 0);
                        return true;
                    }
                case CPUI_FLOAT_FLOAT2FLOAT:
                    {
                        Varnode* vn = op->getIn(0);
                        TransformVar* newvar;
                        OpCode opc;
                        if (vn->isConstant())
                        {
                            opc = CPUI_COPY;
                            if (vn->getSize() == precision)
                                newvar = newConstant(precision, 0, vn->getOffset());
                            else
                            {
                                newvar = setReplacement(vn);    // Convert constant to precision size
                                if (newvar == (TransformVar*)0)
                                    return false;           // Unsupported float format
                            }
                        }
                        else
                        {
                            if (vn->isFree()) return false;
                            opc = (vn->getSize() == precision) ? CPUI_COPY : CPUI_FLOAT_FLOAT2FLOAT;
                            newvar = getPreexistingVarnode(vn);
                        }
                        TransformOp* rop = newOpReplace(1, opc, op);
                        opSetOutput(rop, rvn);
                        opSetInput(rop, newvar, 0);
                        return true;
                    }
                default:
                    break;          // Everything else we abort
            }

            return false;
        }

        /// \brief Push the trace one hop from the placeholder at the top of the worklist
        ///
        /// The logical value for the value on top of the worklist stack is pushed back
        /// to the input Varnodes of the operation defining it.  Then the value is pushed
        /// forward through all operations that read it.
        /// \return \b true if the trace is successfully pushed
        private bool processNextWork()
        {
            TransformVar* rvn = worklist.back();

            worklist.pop_back();

            if (!traceBackward(rvn)) return false;
            return traceForward(rvn);
        }

        /// \param f is the function being transformed
        /// \param root is the start Varnode containing the logical value
        /// \param prec is the precision to assume for the logical value
        public SubfloatFlow(Funcdata f, Varnode root, int4 prec)
            : base(f)
        {
            precision = prec;
            format = f->getArch()->translate->getFloatFormat(precision);
            if (format == (FloatFormat*)0)
                return;
            setReplacement(root);
        }

        public override bool preserveAddress(Varnode vn, int4 bitSize, int4 lsbOffset)
        {
            return vn->isInput();       // Only try to preserve address for input varnodes
        }

        /// Trace logical value as far as possible
        /// The interpretation that the root Varnode contains a logical value with
        /// smaller precision is pushed through the data-flow.  If the interpretation is
        /// inconsistent, \b false is returned.  Otherwise a transform is constructed that
        /// makes the smaller precision the explicit size of Varnodes within the data-flow.
        /// \return \b true if a transform consistent with the given precision can be built
        public bool doTrace()
        {
            if (format == (FloatFormat*)0)
                return false;
            terminatorCount = 0;    // Have seen no terminators
            bool retval = true;
            while (!worklist.empty())
            {
                if (!processNextWork())
                {
                    retval = false;
                    break;
                }
            }

            clearVarnodeMarks();

            if (!retval) return false;
            if (terminatorCount == 0) return false; // Must see at least 1 terminator
            return true;
        }
    }
}
