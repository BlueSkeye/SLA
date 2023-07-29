using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Get rid of registers with trash values.
    ///
    /// Register locations called \b likely \b trash are read as a side-effect of some instruction
    /// the compiler was using.  The canonical example in x86 code is the
    ///     PUSH ECX
    /// which compilers use to create space on the stack without caring about what's in ECX.
    /// Even though the decompiler can see that the read ECX value is never getting used directly
    /// by the function, because the value is getting copied to the stack, the decompiler frequently
    /// can't tell if the value has been aliased across sub-function calls. By marking the ECX register
    /// as \b likely \ trash the decompiler will assume that, unless there is a direct read of the
    /// incoming ECX, none of subfunctions alias the stack location where ECX was stored.  This
    /// allows the spurious references to the register to be removed.
    internal class ActionLikelyTrash : Action
    {
        /// Count the number of inputs to \b op which have their mark set
        /// \param op is the PcodeOp to count
        /// \return the number of marks set
        private static uint countMarks(PcodeOp op)
        {
            uint res = 0;
            for (int i = 0; i < op.numInput(); ++i)
            {
                Varnode* vn = op.getIn(i);
                for (; ; )
                {
                    if (vn.isMark())
                    {
                        res += 1;
                        break;
                    }
                    if (!vn.isWritten()) break;
                    PcodeOp* defOp = vn.getDef();
                    if (defOp == op)
                    {   // We have looped all the way around
                        res += 1;
                        break;
                    }
                    else if (defOp.code() != CPUI_INDIRECT)    // Chain up through INDIRECTs
                        break;
                    vn = vn.getDef().getIn(0);
                }
            }
            return res;
        }

        /// \brief Decide if the given Varnode only ever flows into CPUI_INDIRECT
        ///
        /// Return all the CPUI_INDIRECT ops that the Varnode hits in a list.
        /// Trace forward down all paths from -vn-, if we hit
        ///    - CPUI_INDIRECT  . trim that path and store that op in the resulting -indlist-
        ///    - CPUI_SUBPIECE
        ///    - CPUI_MULTIEQUAL
        ///    - CPUI_PIECE
        ///    - CPUI_AND       . follow through to output
        ///    - anything else  . consider -vn- to NOT be trash
        ///
        /// For any CPUI_MULTIEQUAL and CPUI_PIECE that are hit, all the other inputs must be hit as well
        /// \param vn is the given Varnode
        /// \param indlist is the list to populate with CPUI_INDIRECT ops
        /// \return \b true if all flows look like trash
        private static bool traceTrash(Varnode vn, List<PcodeOp> indlist)
        {
            List<PcodeOp*> allroutes; // Keep track of merging ops (with more than 1 input)
            List<Varnode*> markedlist;    // All varnodes we have visited on paths from -vn-
            list<PcodeOp*>::const_iterator iter, enditer;
            Varnode* outvn;
            ulong val;
            uint traced = 0;
            vn.setMark();
            markedlist.push_back(vn);
            bool istrash = true;

            while (traced < markedlist.size())
            {
                Varnode* curvn = markedlist[traced++];
                iter = curvn.beginDescend();
                enditer = curvn.endDescend();
                for (; iter != enditer; ++iter)
                {
                    PcodeOp* op = *iter;
                    outvn = op.getOut();
                    switch (op.code())
                    {
                        case CPUI_INDIRECT:
                            if (outvn.isPersist())
                                istrash = false;
                            else if (op.isIndirectStore())
                            {
                                if (!outvn.isMark())
                                {
                                    outvn.setMark();
                                    markedlist.push_back(outvn);
                                }
                            }
                            else
                                indlist.push_back(op);
                            break;
                        case CPUI_SUBPIECE:
                            if (outvn.isPersist())
                                istrash = false;
                            else
                            {
                                if (!outvn.isMark())
                                {
                                    outvn.setMark();
                                    markedlist.push_back(outvn);
                                }
                            }
                            break;
                        case CPUI_MULTIEQUAL:
                        case CPUI_PIECE:
                            if (outvn.isPersist())
                                istrash = false;
                            else
                            {
                                if (!op.isMark())
                                {
                                    op.setMark();
                                    allroutes.push_back(op);
                                }
                                uint nummark = countMarks(op);
                                if (nummark == op.numInput())
                                {
                                    if (!outvn.isMark())
                                    {
                                        outvn.setMark();
                                        markedlist.push_back(outvn);
                                    }
                                }
                            }
                            break;
                        case CPUI_INT_AND:
                            // If the AND is using only the topmost significant bytes then it is likely trash
                            if (op.getIn(1).isConstant())
                            {
                                val = op.getIn(1).getOffset();
                                ulong mask = calc_mask(op.getIn(1).getSize());
                                if ((val == ((mask << 8) & mask)) || (val == ((mask << 16) & mask)) || (val == ((mask << 32) & mask)))
                                {
                                    indlist.push_back(op);
                                    break;
                                }
                            }
                            istrash = false;
                            break;
                        default:
                            istrash = false;
                            break;
                    }
                    if (!istrash) break;
                }
                if (!istrash) break;
            }

            for (uint i = 0; i < allroutes.size(); ++i)
            {
                if (!allroutes[i].getOut().isMark())
                    istrash = false;        // Didn't see all inputs
                allroutes[i].clearMark();
            }
            for (uint i = 0; i < markedlist.size(); ++i)
                markedlist[i].clearMark();

            return istrash;
        }

        public ActionLikelyTrash(string g)
            : base(0,"likelytrash", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionLikelyTrash(getGroup());
        }

        public override int apply(Funcdata data)
        {
            List<PcodeOp*> indlist;

            List<VarnodeData>::const_iterator iter, enditer;
            iter = data.getFuncProto().trashBegin();
            enditer = data.getFuncProto().trashEnd();
            for (; iter != enditer; ++iter)
            {
                VarnodeData vdata = *iter;
                Varnode* vn = data.findCoveredInput(vdata.size, vdata.getAddr());
                if (vn == (Varnode*)0) continue;
                if (vn.isTypeLock() || vn.isNameLock()) continue;
                indlist.clear();
                if (!traceTrash(vn, indlist)) continue;

                for (uint i = 0; i < indlist.size(); ++i)
                {
                    PcodeOp* op = indlist[i];
                    if (op.code() == CPUI_INDIRECT)
                    {
                        // Trucate data-flow through INDIRECT, turning it into indirect creation
                        data.opSetInput(op, data.newConstant(op.getOut().getSize(), 0), 0);
                        data.markIndirectCreation(op, false);
                    }
                    else if (op.code() == CPUI_INT_AND)
                    {
                        data.opSetInput(op, data.newConstant(op.getIn(1).getSize(), 0), 1);
                    }
                    count += 1;         // Indicate we made a change
                }
            }
            return 0;
        }
    }
}
