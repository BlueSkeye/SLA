using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class MultForm
    {
        private SplitVarnode @in;
        private PcodeOp add1;
        private PcodeOp add2;
        private PcodeOp subhi;
        private PcodeOp multlo;
        private PcodeOp multhi1;
        private PcodeOp multhi2;
        private Varnode midtmp;
        private PcodeOp lo1zext;
        private PcodeOp lo2zext;
        private Varnode hi1;
        private Varnode lo1;
        private Varnode hi2;
        private Varnode lo2;
        private Varnode reslo;
        private Varnode reshi;
        private SplitVarnode outdoub;
        private SplitVarnode in2;
        private PcodeOp existop;

        private bool zextOf(Varnode big, Varnode small)
        { // Verify that big is (some form of) a zero extension of small
            PcodeOp* op;
            if (small.isConstant())
            {
                if (!big.isConstant()) return false;
                if (big.getOffset() == small.getOffset()) return true;
                return false;
            }
            if (!big.isWritten()) return false;
            op = big.getDef();
            if (op.code() == CPUI_INT_ZEXT)
                return (op.getIn(0) == small);
            if (op.code() == CPUI_INT_AND)
            {
                if (!op.getIn(1).isConstant()) return false;
                if (op.getIn(1).getOffset() != calc_mask(small.getSize())) return false;
                Varnode* whole = op.getIn(0);
                if (!small.isWritten()) return false;
                PcodeOp* sub = small.getDef();
                if (sub.code() != CPUI_SUBPIECE) return false;
                return (sub.getIn(0) == whole);
            }
            return false;
        }

        private bool mapResHi(Varnode rhi)
        { // Find reshi=hi1*lo2 + hi2*lo1 + (tmp>>32)
            reshi = rhi;
            if (!reshi.isWritten()) return false;
            add1 = reshi.getDef();
            if (add1.code() != CPUI_INT_ADD) return false;
            Varnode* ad1,*ad2,*ad3;
            ad1 = add1.getIn(0);
            ad2 = add1.getIn(1);
            if (!ad1.isWritten()) return false;
            if (!ad2.isWritten()) return false;
            add2 = ad1.getDef();
            if (add2.code() == CPUI_INT_ADD)
            {
                ad1 = add2.getIn(0);
                ad3 = add2.getIn(1);
            }
            else
            {
                add2 = ad2.getDef();
                if (add2.code() != CPUI_INT_ADD) return false;
                ad2 = add2.getIn(0);
                ad3 = add2.getIn(1);
            }
            if (!ad1.isWritten()) return false;
            if (!ad2.isWritten()) return false;
            if (!ad3.isWritten()) return false;
            subhi = ad1.getDef();
            if (subhi.code() == CPUI_SUBPIECE)
            {
                multhi1 = ad2.getDef();
                multhi2 = ad3.getDef();
            }
            else
            {
                subhi = ad2.getDef();
                if (subhi.code() == CPUI_SUBPIECE)
                {
                    multhi1 = ad1.getDef();
                    multhi2 = ad3.getDef();
                }
                else
                {
                    subhi = ad3.getDef();
                    if (subhi.code() == CPUI_SUBPIECE)
                    {
                        multhi1 = ad1.getDef();
                        multhi2 = ad2.getDef();
                    }
                    else
                        return false;
                }
            }
            if (multhi1.code() != CPUI_INT_MULT) return false;
            if (multhi2.code() != CPUI_INT_MULT) return false;

            midtmp = subhi.getIn(0);
            if (!midtmp.isWritten()) return false;
            multlo = midtmp.getDef();
            if (multlo.code() != CPUI_INT_MULT) return false;
            lo1zext = multlo.getIn(0);
            lo2zext = multlo.getIn(1);
            return true;
        }

        private bool mapResHiSmallConst(Varnode rhi)
        { // find reshi=hi1*lo2 + (tmp>>32)
            reshi = rhi;
            if (!reshi.isWritten()) return false;
            add1 = reshi.getDef();
            if (add1.code() != CPUI_INT_ADD) return false;
            Varnode* ad1,*ad2;
            ad1 = add1.getIn(0);
            ad2 = add1.getIn(1);
            if (!ad1.isWritten()) return false;
            if (!ad2.isWritten()) return false;
            multhi1 = ad1.getDef();
            if (multhi1.code() != CPUI_INT_MULT)
            {
                subhi = multhi1;
                multhi1 = ad2.getDef();
            }
            else
                subhi = ad2.getDef();
            if (multhi1.code() != CPUI_INT_MULT) return false;
            if (subhi.code() != CPUI_SUBPIECE) return false;
            midtmp = subhi.getIn(0);
            if (!midtmp.isWritten()) return false;
            multlo = midtmp.getDef();
            if (multlo.code() != CPUI_INT_MULT) return false;
            lo1zext = multlo.getIn(0);
            lo2zext = multlo.getIn(1);
            return true;
        }

        private bool findLoFromIn()
        { // Assuming we have -multhi1-, -multhi2-, -lo1-, and -hi1- in hand, try to label lo2/hi2 pair
            Varnode* vn1 = multhi1.getIn(0);
            Varnode* vn2 = multhi1.getIn(1);
            if ((vn1 != lo1) && (vn2 != lo1))
            { // Try to normalize so multhi1 contains lo1
                PcodeOp* tmpop = multhi1;
                multhi1 = multhi2;
                multhi2 = tmpop;
                vn1 = multhi1.getIn(0);
                vn2 = multhi1.getIn(1);
            }
            if (vn1 == lo1)
                hi2 = vn2;
            else if (vn2 == lo1)
                hi2 = vn1;
            else
                return false;
            vn1 = multhi2.getIn(0);    // multhi2 should contain hi1 and lo2
            vn2 = multhi2.getIn(1);
            if (vn1 == hi1)
                lo2 = vn2;
            else if (vn2 == hi1)
                lo2 = vn1;
            else
                return false;

            return true;
        }

        private bool findLoFromInSmallConst()
        { // Assuming we have -multhi1-, -lo1-, and -hi1- in hand, try to label -lo2-
            Varnode* vn1 = multhi1.getIn(0);
            Varnode* vn2 = multhi1.getIn(1);
            if (vn1 == hi1)
                lo2 = vn2;
            else if (vn2 == hi1)
                lo2 = vn1;
            else
                return false;
            if (!lo2.isConstant()) return false;
            hi2 = (Varnode*)0;      // hi2 is an implied zero in this case
            return true;
        }

        private bool verifyLo()
        { // Given we have labelled lo1/hi1 lo2/hi2, make sure midtmp is formed properly
          // This also works for the small constant model  lo1/hi1 and lo2 const.
            if (subhi.getIn(1).getOffset() != lo1.getSize()) return false;
            if (zextOf(lo1zext, lo1))
            {
                if (zextOf(lo2zext, lo2))
                    return true;
            }
            else if (zextOf(lo1zext, lo2))
            {
                if (zextOf(lo2zext, lo1))
                    return true;
            }
            return false;
        }

        private bool findResLo()
        { // Assuming we found -midtmp-, find potential reslo
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = midtmp.beginDescend();
            enditer = midtmp.endDescend();
            while (iter != enditer)
            {
                PcodeOp* op = *iter;
                ++iter;
                if (op.code() != CPUI_SUBPIECE) continue;
                if (op.getIn(1).getOffset() != 0) continue; // Must grab low bytes
                reslo = op.getOut();
                if (reslo.getSize() != lo1.getSize()) continue;
                return true;
            }
            // If we reach here, it may be that separate multiplies of lo1*lo2 were used for reshi and reslo
            iter = lo1.beginDescend();
            enditer = lo1.endDescend();
            while (iter != enditer)
            {
                PcodeOp* op = *iter;
                ++iter;
                if (op.code() != CPUI_INT_MULT) continue;
                Varnode* vn1 = op.getIn(0);
                Varnode* vn2 = op.getIn(1);
                if (lo2.isConstant())
                {
                    if ((!vn1.isConstant() || (vn1.getOffset() != lo2.getOffset())) &&
                    (!vn2.isConstant() || (vn2.getOffset() != lo2.getOffset())))
                        continue;
                }
                else
                  if ((op.getIn(0) != lo2) && (op.getIn(1) != lo2)) continue;
                reslo = op.getOut();
                return true;
            }
            return false;
        }

        private bool mapFromIn(Varnode rhi)
        { // Try to do full mapping from -in- given a putative reshi
            if (!mapResHi(rhi)) return false;
            if (!findLoFromIn()) return false;
            if (!verifyLo()) return false;
            if (!findResLo()) return false;
            return true;
        }

        private bool mapFromInSmallConst(Varnode rhi)
        {
            if (!mapResHiSmallConst(rhi)) return false;
            if (!findLoFromInSmallConst()) return false;
            if (!verifyLo()) return false;
            if (!findResLo()) return false;
            return true;
        }

        private bool replace(Funcdata data)
        { // We have matched a double precision multiply, now transform to logical variables
            outdoub.initPartial(in.getSize(), reslo, reshi);
            in2.initPartial(in.getSize(), lo2, hi2);
            existop = SplitVarnode::prepareBinaryOp(outdoub, in, in2);
            if (existop == (PcodeOp*)0)
                return false;
            SplitVarnode::createBinaryOp(data, outdoub, in, in2, existop, CPUI_INT_MULT);
            return true;
        }

        public bool verify(Varnode h, Varnode l, PcodeOp hop)
        {
            hi1 = h;
            lo1 = l;
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = hop.getOut().beginDescend();
            enditer = hop.getOut().endDescend();
            while (iter != enditer)
            {
                add1 = *iter;
                ++iter;
                if (add1.code() != CPUI_INT_ADD) continue;
                list<PcodeOp*>::const_iterator iter2, enditer2;
                iter2 = add1.getOut().beginDescend();
                enditer2 = add1.getOut().endDescend();
                while (iter2 != enditer2)
                {
                    add2 = *iter2;
                    ++iter2;
                    if (add2.code() != CPUI_INT_ADD) continue;
                    if (mapFromIn(add2.getOut()))
                        return true;
                }
                if (mapFromIn(add1.getOut()))
                    return true;
                if (mapFromInSmallConst(add1.getOut()))
                    return true;
            }
            return false;
        }

        public bool applyRule(SplitVarnode i, PcodeOp hop, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;

            if (!verify(@in.getHi(), @in.getLo(), hop))
                return false;

            if (replace(data)) return true;
            return false;
        }
    }
}
