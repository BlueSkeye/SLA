using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Perform Common Sub-expression Elimination on CPUI_MULTIEQUAL ops
    internal class ActionMultiCse : Action
    {
        /// Which of two outputs is preferred
        /// We are substituting either -out1- for -out2-  OR  -out2- for -out1-
        /// Return true if we prefer substituting -out2- for -out1-
        /// \param out1 is one output
        /// \param out2 is the other output
        /// \return preference
        private static bool preferredOutput(Varnode out1, Varnode out2)
        {
            // Prefer the output that is used in a CPUI_RETURN
            list<PcodeOp*>::const_iterator iter, enditer;
            enditer = out1->endDescend();
            for (iter = out1->beginDescend(); iter != enditer; ++iter)
            {
                PcodeOp* op = *iter;
                if (op->code() == CPUI_RETURN)
                    return false;
            }
            enditer = out2->endDescend();
            for (iter = out2->beginDescend(); iter != enditer; ++iter)
            {
                PcodeOp* op = *iter;
                if (op->code() == CPUI_RETURN)
                    return true;
            }
            // Prefer addrtied over register over unique
            if (!out1->isAddrTied())
            {
                if (out2->isAddrTied())
                    return true;
                else
                {
                    if (out1->getSpace()->getType() == IPTR_INTERNAL)
                    {
                        if (out2->getSpace()->getType() != IPTR_INTERNAL)
                            return true;
                    }
                }
            }
            return false;
        }

        /// Find match to CPUI_MULTIEQUAL
        /// Find any matching CPUI_MULTIEQUAL that occurs before \b target that has \b in as an input.
        /// Then test to see if the \b target and the recovered op are functionally equivalent.
        /// \param bl is the parent block
        /// \param target is the given target CPUI_MULTIEQUAL
        /// \param in is the specific input Varnode
        private static PcodeOp findMatch(BlockBasic bl, PcodeOp target, Varnode @in)
        {
            list<PcodeOp*>::iterator iter = bl->beginOp();

            for (; ; )
            {
                PcodeOp* op = *iter;
                ++iter;
                if (op == target)       // Caught up with target, nothing else before it
                    break;
                int4 i, numinput;
                numinput = op->numInput();
                for (i = 0; i < numinput; ++i)
                {
                    Varnode* vn = op->getIn(i);
                    if (vn->isWritten() && (vn->getDef()->code() == CPUI_COPY))
                        vn = vn->getDef()->getIn(0);        // Allow for differences in copy propagation
                    if (vn == @in) break;
                }
                if (i < numinput) {
                    int4 j;
                    Varnode* buf1[2];
                    Varnode* buf2[2];
                    for (j = 0; j < numinput; ++j) {
                        Varnode* in1 = op->getIn(j);
                        if (in1->isWritten() && (in1->getDef()->code() == CPUI_COPY))
                            in1 = in1->getDef()->getIn(0);    // Allow for differences in copy propagation
                        Varnode* in2 = target->getIn(j);
                        if (in2->isWritten() && (in2->getDef()->code() == CPUI_COPY))
                            in2 = in2->getDef()->getIn(0);
                        if (in1 == in2) continue;
                        if (0 != functionalEqualityLevel(in1, in2, buf1, buf2))
                            break;
                    }
                    if (j == numinput)      // We have found a redundancy
                        return op;
                }
            }
            return (PcodeOp*)0;
        }

        /// Search a block for equivalent CPUI_MULTIEQUAL
        /// Search for pairs of CPUI_MULTIEQUAL ops in \b bl that share an input.
        /// If the pairs found are functionally equivalent, delete one of the two.
        /// \param data is the function owning the block
        /// \param bl is the specific basic block
        /// return \b true if a CPUI_MULTIEQUAL was (successfully) deleted
        private bool processBlock(Funcdata data, BlockBasic bl)
        {
            vector<Varnode*> vnlist;
            PcodeOp* targetop = (PcodeOp*)0;
            PcodeOp* pairop;
            list<PcodeOp*>::iterator iter = bl->beginOp();
            list<PcodeOp*>::iterator enditer = bl->endOp();
            while (iter != enditer)
            {
                PcodeOp* op = *iter;
                ++iter;
                OpCode opc = op->code();
                if (opc == CPUI_COPY) continue;
                if (opc != CPUI_MULTIEQUAL) break;
                int4 vnpos = vnlist.size();
                int4 i;
                int4 numinput = op->numInput();
                for (i = 0; i < numinput; ++i)
                {
                    Varnode* vn = op->getIn(i);
                    if (vn->isWritten() && (vn->getDef()->code() == CPUI_COPY)) // Some copies may not propagate into MULTIEQUAL
                        vn = vn->getDef()->getIn(0);                    // Allow for differences in copy propagation
                    vnlist.push_back(vn);
                    if (vn->isMark())
                    {       // If we've seen this varnode before
                        pairop = findMatch(bl, op, vn);
                        if (pairop != (PcodeOp*)0)
                            break;
                    }
                }
                if (i < numinput)
                {
                    targetop = op;
                    break;
                }
                for (i = vnpos; i < vnlist.size(); ++i)
                    vnlist[i]->setMark();       // Mark that we have seen this varnode
            }

            // Clear out any of the marks we put down
            for (int4 i = 0; i < vnlist.size(); ++i)
                vnlist[i]->clearMark();

            if (targetop != (PcodeOp*)0)
            {
                Varnode* out1 = pairop->getOut();
                Varnode* out2 = targetop->getOut();
                if (preferredOutput(out1, out2))
                {
                    data.totalReplace(out1, out2);  // Replace pairop and out1 in favor of targetop and out2
                    data.opDestroy(pairop);
                }
                else
                {
                    data.totalReplace(out2, out1);
                    data.opDestroy(targetop);
                }
                count += 1;     // Indicate that a change has taken place
                return true;
            }
            return false;
        }

        /// Constructor
        public ActionMultiCse(string g)
            : base(0,"multicse", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMultiCse(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            const BlockGraph &bblocks(data.getBasicBlocks());
            int4 sz = bblocks.getSize();
            for (int4 i = 0; i < sz; ++i)
            {
                BlockBasic* bl = (BlockBasic*)bblocks.getBlock(i);
                while (processBlock(data, bl))
                {
                }
            }
            return 0;
        }
    }
}
