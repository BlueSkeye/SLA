using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePtrFlow : Rule
    {
        /// The address space manager
        private Architecture glb;
        /// \b true if this architecture needs truncated pointers
        private bool hasTruncations;

        /// Set \e ptrflow property on PcodeOp only if it is propagating
        ///
        /// \param op is the PcodeOp
        /// \return \b true if ptrflow property is newly set
        private bool trialSetPtrFlow(PcodeOp op)
        {
            switch (op->code())
            {
                case CPUI_COPY:
                case CPUI_MULTIEQUAL:
                case CPUI_INT_ADD:
                case CPUI_INDIRECT:
                case CPUI_PTRSUB:
                case CPUI_PTRADD:
                    if (!op->isPtrFlow())
                    {
                        op->setPtrFlow();
                        return true;
                    }
                    break;
                default:
                    break;
            }
            return false;
        }

        /// \brief Propagate \e ptrflow property to given Varnode and the defining PcodeOp
        ///
        /// \param vn is the given Varnode
        /// \return \b true if a change was made
        private bool propagateFlowToDef(Varnode vn)
        {
            bool madeChange = false;
            if (!vn->isPtrFlow())
            {
                vn->setPtrFlow();
                madeChange = true;
            }
            if (!vn->isWritten()) return madeChange;
            PcodeOp* op = vn->getDef();
            if (trialSetPtrFlow(op))
                madeChange = true;
            return madeChange;
        }

        /// \brief Propagate \e ptrflow property to given Varnode and to descendant PcodeOps
        ///
        /// \param vn is the given Varnode
        /// \return \b true if a change was made
        private bool propagateFlowToReads(Varnode vn)
        {
            list<PcodeOp*>::const_iterator iter;
            bool madeChange = false;
            if (!vn->isPtrFlow())
            {
                vn->setPtrFlow();
                madeChange = true;
            }
            for (iter = vn->beginDescend(); iter != vn->endDescend(); ++iter)
            {
                PcodeOp* op = *iter;
                if (trialSetPtrFlow(op))
                    madeChange = true;
            }
            return madeChange;
        }

        /// \brief Truncate pointer Varnode being read by given PcodeOp
        ///
        /// Insert a SUBPIECE operation truncating the value to the size necessary
        /// for a pointer into the given address space. Update the PcodeOp input.
        /// \param spc is the given address space
        /// \param op is the given PcodeOp reading the pointer
        /// \param vn is the pointer Varnode
        /// \param slot is the input slot reading the pointer
        /// \param data is the function being analyzed
        /// \return the new truncated Varnode
        private Varnode truncatePointer(AddrSpace spc, PcodeOp op, Varnode vn, int4 slot, Funcdata data)
        {
            Varnode* newvn;
            PcodeOp* truncop = data.newOp(2, op->getAddr());
            data.opSetOpcode(truncop, CPUI_SUBPIECE);
            data.opSetInput(truncop, data.newConstant(vn->getSize(), 0), 1);
            if (vn->getSpace()->getType() == IPTR_INTERNAL)
            {
                newvn = data.newUniqueOut(spc->getAddrSize(), truncop);
            }
            else
            {
                Address addr = vn->getAddr();
                if (addr.isBigEndian())
                    addr = addr + (vn->getSize() - spc->getAddrSize());
                addr.renormalize(spc->getAddrSize());
                newvn = data.newVarnodeOut(spc->getAddrSize(), addr, truncop);
            }
            data.opSetInput(op, newvn, slot);
            data.opSetInput(truncop, vn, 0);
            data.opInsertBefore(truncop, op);
            return newvn;
        }

        /// \class RulePtrFlow
        /// \brief Mark Varnode and PcodeOp objects that are carrying or operating on pointers
        ///
        /// This is used on architectures where the data-flow for pointer values needs to be
        /// truncated.  This marks the places where the truncation needs to happen.  Then
        /// the SubvariableFlow actions do the actual truncation.
        public RulePtrFlow(string g, Architecture conf)
            : base(g, 0, "ptrflow")
        {
            glb = conf;
            hasTruncations = glb->getDefaultDataSpace()->isTruncated();
        }

        private virtual Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePtrFlow(getGroup(), glb);
        }

        private void getOpList(List<uint4> oplist)
        {
            if (!hasTruncations) return;    // Only stick ourselves into pool if aggresiveness is turned on
            oplist.push_back(CPUI_STORE);
            oplist.push_back(CPUI_LOAD);
            oplist.push_back(CPUI_COPY);
            oplist.push_back(CPUI_MULTIEQUAL);
            oplist.push_back(CPUI_INDIRECT);
            oplist.push_back(CPUI_INT_ADD);
            oplist.push_back(CPUI_CALLIND);
            oplist.push_back(CPUI_BRANCHIND);
            oplist.push_back(CPUI_PTRSUB);
            oplist.push_back(CPUI_PTRADD);
        }

        private int4 applyOp(PcodeOp op, Funcdata data)
        { // Push pointer-ness 
            Varnode* vn;
            AddrSpace* spc;
            int4 madeChange = 0;

            switch (op->code())
            {
                case CPUI_LOAD:
                case CPUI_STORE:
                    vn = op->getIn(1);
                    spc = op->getIn(0)->getSpaceFromConst();
                    if (vn->getSize() > spc->getAddrSize())
                    {
                        vn = truncatePointer(spc, op, vn, 1, data);
                        madeChange = 1;
                    }
                    if (propagateFlowToDef(vn))
                        madeChange = 1;
                    break;
                case CPUI_CALLIND:
                case CPUI_BRANCHIND:
                    vn = op->getIn(0);
                    spc = data.getArch()->getDefaultCodeSpace();
                    if (vn->getSize() > spc->getAddrSize())
                    {
                        vn = truncatePointer(spc, op, vn, 0, data);
                        madeChange = 1;
                    }
                    if (propagateFlowToDef(vn))
                        madeChange = 1;
                    break;
                case CPUI_NEW:
                    vn = op->getOut();
                    if (propagateFlowToReads(vn))
                        madeChange = 1;
                    break;
                case CPUI_INDIRECT:
                    if (!op->isPtrFlow()) return 0;
                    vn = op->getOut();
                    if (propagateFlowToReads(vn))
                        madeChange = 1;
                    vn = op->getIn(0);
                    if (propagateFlowToDef(vn))
                        madeChange = 1;
                    break;
                case CPUI_COPY:
                case CPUI_PTRSUB:
                case CPUI_PTRADD:
                    if (!op->isPtrFlow()) return 0;
                    vn = op->getOut();
                    if (propagateFlowToReads(vn))
                        madeChange = 1;
                    vn = op->getIn(0);
                    if (propagateFlowToDef(vn))
                        madeChange = 1;
                    break;
                case CPUI_MULTIEQUAL:
                case CPUI_INT_ADD:
                    if (!op->isPtrFlow()) return 0;
                    vn = op->getOut();
                    if (propagateFlowToReads(vn))
                        madeChange = 1;
                    for (int4 i = 0; i < op->numInput(); ++i)
                    {
                        vn = op->getIn(i);
                        if (propagateFlowToDef(vn))
                            madeChange = 1;
                    }
                    break;
                default:
                    break;
            }
            return madeChange;
        }
    }
}
