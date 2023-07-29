﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePiecePathology : Rule
    {
        /// \brief Return \b true if concatenating with a SUBPIECE of the given Varnode is unusual
        ///
        /// \param vn is the given Varnode
        /// \param data is the function containing the Varnode
        /// \return \b true if the configuration is a pathology
        private static bool isPathology(Varnode vn, Funcdata data)
        {
            vector<PcodeOp*> worklist;
            int4 pos = 0;
            int4 slot = 0;
            bool res = false;
            for (; ; )
            {
                if (vn->isInput() && !vn->isPersist())
                {
                    res = true;
                    break;
                }
                PcodeOp* op = vn->getDef();
                while (!res && op != (PcodeOp*)0)
                {
                    switch (op->code())
                    {
                        case CPUI_COPY:
                            vn = op->getIn(0);
                            op = vn->getDef();
                            break;
                        case CPUI_MULTIEQUAL:
                            if (!op->isMark())
                            {
                                op->setMark();
                                worklist.push_back(op);
                            }
                            op = (PcodeOp*)0;
                            break;
                        case CPUI_INDIRECT:
                            if (op->getIn(1)->getSpace()->getType() == IPTR_IOP)
                            {
                                PcodeOp* callOp = PcodeOp::getOpFromConst(op->getIn(1)->getAddr());
                                if (callOp->isCall())
                                {
                                    FuncCallSpecs* fspec = data.getCallSpecs(callOp);
                                    if (fspec != (FuncCallSpecs*)0 && !fspec->isOutputActive())
                                    {
                                        res = true;
                                    }
                                }
                            }
                            op = (PcodeOp*)0;
                            break;
                        case CPUI_CALL:
                        case CPUI_CALLIND:
                            {
                                FuncCallSpecs* fspec = data.getCallSpecs(op);
                                if (fspec != (FuncCallSpecs*)0 && !fspec->isOutputActive())
                                {
                                    res = true;
                                }
                                break;
                            }
                        default:
                            op = (PcodeOp*)0;
                            break;
                    }
                }
                if (res) break;
                if (pos >= worklist.size()) break;
                op = worklist[pos];
                if (slot < op->numInput())
                {
                    vn = op->getIn(slot);
                    slot += 1;
                }
                else
                {
                    pos += 1;
                    if (pos >= worklist.size()) break;
                    vn = worklist[pos]->getIn(0);
                    slot = 1;
                }
            }
            for (int4 i = 0; i < worklist.size(); ++i)
                worklist[i]->clearMark();
            return res;
        }

        /// \brief Given a known pathological concatenation, trace it forward to CALLs and RETURNs
        ///
        /// If the pathology reaches a CALL or RETURN, it is noted, through the FuncProto or FuncCallSpecs
        /// object, that the parameter or return value is only partially consumed.  The subvariable flow
        /// rules can then decide whether or not to truncate this part of the data-flow.
        /// \param op is CPUI_PIECE op that is the pathological concatenation
        /// \param data is the function containing the data-flow
        /// \return a non-zero value if new bytes are labeled as unconsumed
        private static int4 tracePathologyForward(PcodeOp op, Funcdata data)
        {
            int4 count = 0;
            const FuncCallSpecs* fProto;
            vector<PcodeOp*> worklist;
            int4 pos = 0;
            op->setMark();
            worklist.push_back(op);
            while (pos < worklist.size())
            {
                PcodeOp* curOp = worklist[pos];
                pos += 1;
                Varnode* outVn = curOp->getOut();
                list<PcodeOp*>::const_iterator iter;
                list<PcodeOp*>::const_iterator enditer = outVn->endDescend();
                for (iter = outVn->beginDescend(); iter != enditer; ++iter)
                {
                    curOp = *iter;
                    switch (curOp->code())
                    {
                        case CPUI_COPY:
                        case CPUI_INDIRECT:
                        case CPUI_MULTIEQUAL:
                            if (!curOp->isMark())
                            {
                                curOp->setMark();
                                worklist.push_back(curOp);
                            }
                            break;
                        case CPUI_CALL:
                        case CPUI_CALLIND:
                            fProto = data.getCallSpecs(curOp);
                            if (fProto != (FuncProto*)0 && !fProto->isInputActive() && !fProto->isInputLocked())
                            {
                                int4 bytesConsumed = op->getIn(1)->getSize();
                                for (int4 i = 1; i < curOp->numInput(); ++i)
                                {
                                    if (curOp->getIn(i) == outVn)
                                    {
                                        if (fProto->setInputBytesConsumed(i, bytesConsumed))
                                            count += 1;
                                    }
                                }
                            }
                            break;
                        case CPUI_RETURN:
                            if (!data.getFuncProto().isOutputLocked())
                            {
                                if (data.getFuncProto().setReturnBytesConsumed(op->getIn(1)->getSize()))
                                    count += 1;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            for (int4 i = 0; i < worklist.size(); ++i)
                worklist[i]->clearMark();
            return count;
        }

        public RulePiecePathology(string g)
            : base(g, 0, "piecepathology")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePiecePathology(getGroup());
        }

        /// \class RulePiecePathology
        /// \brief Search for concatenations with unlikely things to inform return/parameter consumption calculation
        ///
        /// For that can read/write part of a general purpose register, a small return value can get concatenated
        /// with unrelated data when the function writes directly to part of the return register. This searches
        /// for a characteristic pathology:
        /// \code
        ///     retreg = CALL();
        ///     ...
        ///     retreg = CONCAT(SUBPIECE(retreg,#4),smallval);
        /// \endcode
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_PIECE);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op->getIn(0);
            if (!vn->isWritten()) return 0;
            PcodeOp* subOp = vn->getDef();

            // Make sure we are concatenating the most significant bytes of a truncation
            OpCode opc = subOp->code();
            if (opc == CPUI_SUBPIECE)
            {
                if (subOp->getIn(1)->getOffset() == 0) return 0;
                if (!isPathology(subOp->getIn(0), data)) return 0;
            }
            else if (opc == CPUI_INDIRECT)
            {
                if (!subOp->isIndirectCreation()) return 0;                 // Indirect concatenation
                Varnode* lsbVn = op->getIn(1);
                if (!lsbVn->isWritten()) return 0;
                PcodeOp* lsbOp = lsbVn->getDef();
                if ((lsbOp->getEvalType() & (PcodeOp::binary | PcodeOp::unary)) == 0)
                {   // from either a unary/binary operation
                    if (!lsbOp->isCall()) return 0;                     // or a CALL
                    FuncCallSpecs* fc = data.getCallSpecs(lsbOp);
                    if (fc == (FuncCallSpecs*)0) return 0;
                    if (!fc->isOutputLocked()) return 0;                    // with a locked output
                }
                Address addr = lsbVn->getAddr();
                if (addr.getSpace()->isBigEndian())
                    addr = addr - vn->getSize();
                else
                    addr = addr + lsbVn->getSize();
                if (addr != vn->getAddr()) return 0;                    // into a contiguous register
            }
            else
                return 0;
            return tracePathologyForward(op, data);
        }
    }
}