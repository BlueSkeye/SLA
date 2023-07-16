using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    internal class AddForm
    {
        private SplitVarnode @in;
        private Varnode hi1;
        private Varnode hi2;
        private Varnode lo1;
        private Varnode lo2;
        private Varnode reshi;
        private Varnode reslo;
        private PcodeOp zextop;
        private PcodeOp loadd;
        private PcodeOp add2;
        private Varnode hizext1;
        private Varnode hizext2;
        private int4 slot1;
        private uintb negconst;
        private PcodeOp existop;
        private SplitVarnode indoub;
        private SplitVarnode outdoub;

        private bool checkForCarry(PcodeOp op)
        { // If -op- matches a CARRY construction based on lo1 (i.e. CARRY(x,lo1) )
          //    set lo1 (and negconst if lo1 is a constant) to be the corresponding part of the carry
          //    and return true
            if (op->code() != CPUI_INT_ZEXT) return false;
            if (!op->getIn(0)->isWritten()) return false;

            PcodeOp* carryop = op->getIn(0)->getDef();
            if (carryop->code() == CPUI_INT_CARRY)
            { // Normal CARRY form
                if (carryop->getIn(0) == lo1)
                    lo2 = carryop->getIn(1);
                else if (carryop->getIn(1) == lo1)
                    lo2 = carryop->getIn(0);
                else
                    return false;
                if (lo2->isConstant()) return false;
                return true;
            }
            if (carryop->code() == CPUI_INT_LESS)
            { // Possible CARRY
                Varnode* tmpvn = carryop->getIn(0);
                if (tmpvn->isConstant())
                {
                    if (carryop->getIn(1) != lo1) return false;
                    negconst = tmpvn->getOffset();
                    // In constant forms, the <= will get converted to a <
                    // Note the lessthan to less conversion adds 1 then the 2's complement subtracts 1 and negates
                    // So all we really need to do is negate
                    negconst = (~negconst) & calc_mask(lo1->getSize());
                    lo2 = (Varnode*)0;
                    return true;
                }
                else if (tmpvn->isWritten())
                {   // Calculate CARRY relative to result of loadd
                    PcodeOp* loadd_op = tmpvn->getDef();    // This is the putative loadd
                    if (loadd_op->code() != CPUI_INT_ADD) return false;
                    Varnode* othervn;
                    if (loadd_op->getIn(0) == lo1)
                        othervn = loadd_op->getIn(1);
                    else if (loadd_op->getIn(1) == lo1)
                        othervn = loadd_op->getIn(0);
                    else
                        return false;           // One side of the add must be lo1
                    if (othervn->isConstant())
                    {
                        negconst = othervn->getOffset();
                        lo2 = (Varnode*)0;
                        Varnode* relvn = carryop->getIn(1);
                        if (relvn == lo1) return true;  // Comparison can be relative to lo1
                        if (!relvn->isConstant()) return false;
                        if (relvn->getOffset() != negconst) return false;   // Otherwise must be relative to (constant)lo2
                        return true;
                    }
                    else
                    {
                        lo2 = othervn;      // Other side of putative loadd must be lo2
                        Varnode* compvn = carryop->getIn(1);
                        if ((compvn == lo2) || (compvn == lo1))
                            return true;
                    }
                }
                return false;
            }
            if (carryop->code() == CPUI_INT_NOTEQUAL)
            { // Possible CARRY against -1
                if (!carryop->getIn(1)->isConstant()) return false;
                if (carryop->getIn(0) != lo1) return false;
                if (carryop->getIn(1)->getOffset() != 0) return false;
                negconst = calc_mask(lo1->getSize()); // Original CARRY constant must have been -1
                lo2 = (Varnode*)0;
                return true;
            }
            return false;
        }

        // Given a known double precision input, look for a double precision add,
        // recovering the other double input and the double output
        //
        // Assume hi1, lo1 is a known double precision pair, we look for
        //   reshi = hi1 + hi2 + hizext             (2 variants here)
        //   hizext = zext(bool)
        //   {                                       (2 variants)
        //     bool = (-lo1 <= lo2)     OR
        //     bool = (-lo2 <= lo1)                  (multiple ways to calculate negation)
        //   }
        //   reslo = lo1 + lo2
        public bool verify(Varnode h, Varnode l, PcodeOp op)
        {
            hi1 = h;
            lo1 = l;
            slot1 = op->getSlot(hi1);
            for (int4 i = 0; i < 3; ++i)
            {
                if (i == 0)
                {       // Assume we have to descend one more add
                    add2 = op->getOut()->loneDescend();
                    if (add2 == (PcodeOp*)0) continue;
                    if (add2->code() != CPUI_INT_ADD) continue;
                    reshi = add2->getOut();
                    hizext1 = op->getIn(1 - slot1);
                    hizext2 = add2->getIn(1 - add2->getSlot(op->getOut()));
                }
                else if (i == 1)
                {       // Assume we are at the bottom most of two adds
                    Varnode* tmpvn = op->getIn(1 - slot1);
                    if (!tmpvn->isWritten()) continue;
                    add2 = tmpvn->getDef();
                    if (add2->code() != CPUI_INT_ADD) continue;
                    reshi = op->getOut();
                    hizext1 = add2->getIn(0);
                    hizext2 = add2->getIn(1);
                }
                else
                {           // Assume there is only one add, with second implied add by 0
                    reshi = op->getOut();
                    hizext1 = op->getIn(1 - slot1);
                    hizext2 = (Varnode*)0;
                }
                for (int4 j = 0; j < 2; ++j)
                {
                    if (i == 2)
                    {       // hi2 is an implied 0
                        if (!hizext1->isWritten()) continue;
                        zextop = hizext1->getDef();
                        hi2 = (Varnode*)0;
                    }
                    else if (j == 0)
                    {
                        if (!hizext1->isWritten()) continue;
                        zextop = hizext1->getDef();
                        hi2 = hizext2;
                    }
                    else
                    {
                        if (!hizext2->isWritten()) continue;
                        zextop = hizext2->getDef();
                        hi2 = hizext1;
                    }
                    if (!checkForCarry(zextop)) continue; // Calculate lo2 and negconst

                    list<PcodeOp*>::const_iterator iter2, enditer2;
                    iter2 = lo1->beginDescend();
                    enditer2 = lo1->endDescend();
                    while (iter2 != enditer2)
                    {
                        loadd = *iter2;
                        ++iter2;
                        if (loadd->code() != CPUI_INT_ADD) continue;
                        Varnode* tmpvn = loadd->getIn(1 - loadd->getSlot(lo1));
                        if (lo2 == (Varnode*)0)
                        {
                            if (!tmpvn->isConstant()) continue;
                            if (tmpvn->getOffset() != negconst) continue;   // Must add same constant used to calculate CARRY
                            lo2 = tmpvn;
                        }
                        else if (lo2->isConstant())
                        {
                            if (!tmpvn->isConstant()) continue;
                            if (lo2->getOffset() != tmpvn->getOffset()) continue;
                        }
                        else if (loadd->getIn(1 - loadd->getSlot(lo1)) != lo2) // Must add same value used to calculate CARRY
                            continue;
                        reslo = loadd->getOut();
                        return true;
                    }
                }
            }
            return false;
        }

        public bool applyRule(SplitVarnode i, PcodeOp op, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;
            if (!verify(@in.getHi(), @in.getLo(), op))
                return false;

            indoub.initPartial(@in.getSize(), lo2, hi2);
            outdoub.initPartial(@in.getSize(), reslo, reshi);
            existop = SplitVarnode.prepareBinaryOp(outdoub, @in, indoub);
            if (existop == (PcodeOp*)0)
                return false;
            SplitVarnode.createBinaryOp(data, outdoub, @in, indoub, existop, CPUI_INT_ADD);
            return true;
        }
    }
}
