using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class LogicalForm
    {
        private SplitVarnode @in;
        private PcodeOp loop;
        private PcodeOp hiop;
        private Varnode hi1;
        private Varnode hi2;
        private Varnode lo1;
        private Varnode lo2;
        private PcodeOp existop;
        private SplitVarnode indoub;
        private SplitVarnode outdoub;

        private int findHiMatch()
        { // Look for the op computing the most significant part of the result for which -loop- computes
          // the least significant part,  look for a known double precis out, then look for known double
          // precis @in.  If the other input is constant, look for a unique op that might be computing the high,
          // Return 0 if we found an op, return -1, if we can't find an op, return -2 if no op exists
            Varnode lo1Tmp = @@in.getLo();
            Varnode vn2 = loop.getIn(1 - loop.getSlot(lo1Tmp));

            SplitVarnode @out = new SplitVarnode();
            if (@out.inHandLoOut(lo1Tmp)) {
                // If we already know what the double precision output looks like
                Varnode hi = @out.getHi();
                if (hi.isWritten()) {
                    // Just look at construction of hi precisi
                    PcodeOp maybeop = hi.getDef();
                    if (maybeop.code() == loop.code()) {
                        if (maybeop.getIn(0) == hi1) {
                            if (maybeop.getIn(1).isConstant() == vn2.isConstant()) {
                                hiop = maybeop;
                                return 0;
                            }
                        }
                        else if (maybeop.getIn(1) == hi1) {
                            if (maybeop.getIn(0).isConstant() == vn2.isConstant()) {
                                hiop = maybeop;
                                return 0;
                            }
                        }
                    }
                }
            }

            if (!vn2.isConstant()) {
                SplitVarnode in2 = new SplitVarnode();
                if (in2.inHandLo(vn2)) {
                    // If we already know what the other double precision input looks like
                    IEnumerator<PcodeOp> iter;
                    IEnumerator<PcodeOp> enditer;
                    iter = in2.getHi().beginDescend();
                    enditer = in2.getHi().endDescend();
                    while (iter != enditer)
                    {
                        PcodeOp* maybeop = *iter;
                        ++iter;
                        if (maybeop.code() == loop.code())
                        {
                            if ((maybeop.getIn(0) == hi1) || (maybeop.getIn(1) == hi1))
                            {
                                hiop = maybeop;
                                return 0;
                            }
                        }
                    }
                }
                return -1;
            }
            else
            {
                list<PcodeOp*>::const_iterator iter, enditer;
                iter = hi1.beginDescend();
                enditer = hi1.endDescend();
                int count = 0;
                PcodeOp* lastop = (PcodeOp)null;
                while (iter != enditer)
                {
                    PcodeOp* maybeop = *iter;
                    ++iter;
                    if (maybeop.code() == loop.code())
                    {
                        if (maybeop.getIn(1).isConstant())
                        {
                            count += 1;
                            if (count > 1) break;
                            lastop = maybeop;
                        }
                    }
                }
                if (count == 1)
                {
                    hiop = lastop;
                    return 0;
                }
                if (count > 1)
                    return -1;      // Couldn't distinguish between multiple possibilities
            }
            return -2;
        }

        // Given a known double precision input, look for a double precision logical operation
        // recovering the other double input and the double output
        //
        // Assume hi1, lo1 is a known double precision pair, we look for
        // reshi = hi1 & hi2
        // reslo = lo1 & lo2
        public bool verify(Varnode h, Varnode l, PcodeOp lop)
        {
            loop = lop;
            lo1 = l;
            hi1 = h;
            int res = findHiMatch();

            if (res == 0)
            {       // We found a matching lo presis operation
                lo2 = loop.getIn(1 - loop.getSlot(lo1));
                hi2 = hiop.getIn(1 - hiop.getSlot(hi1));
                if ((lo2 == lo1) || (lo2 == hi1) || (hi2 == hi1) || (hi2 == lo1)) return false; // No manipulation of itself
                if (lo2 == hi2) return false;
                return true;
            }
            return false;
        }

        private bool applyRule(SplitVarnode i, PcodeOp lop, bool workishi, Funcdata data)
        {
            if (workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;

            if (!verify(@@in.getHi(), @@in.getLo(), lop))
                return false;

            outdoub.initPartial(@@in.getSize(), loop.getOut(), hiop.getOut());
            indoub.initPartial(@@in.getSize(), lo2, hi2);
            existop = SplitVarnode::prepareBinaryOp(outdoub, @in, indoub);
            if (existop == (PcodeOp)null)
                return false;

            SplitVarnode::createBinaryOp(data, outdoub, @in, indoub, existop, loop.code());
            return true;
        }
    }
}
