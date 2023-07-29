using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class Equal3Form
    {
        private SplitVarnode @in;
        private Varnode hi;
        private Varnode lo;
        private PcodeOp andop;
        private PcodeOp compareop;
        private Varnode smallc;
        
        public bool verify(Varnode h, Varnode l, PcodeOp aop)
        {
            if (aop->code() != CPUI_INT_AND) return false;
            hi = h;
            lo = l;
            andop = aop;
            int4 hislot = andop->getSlot(hi);
            if (andop->getIn(1 - hislot) != lo) return false;   // hi and lo must be ANDed together
            compareop = andop->getOut()->loneDescend();
            if (compareop == (PcodeOp*)0) return false;
            if ((compareop->code() != CPUI_INT_EQUAL) && (compareop->code() != CPUI_INT_NOTEQUAL))
                return false;
            uintb allonesval = calc_mask(lo->getSize());
            smallc = compareop->getIn(1);
            if (!smallc->isConstant()) return false;
            if (smallc->getOffset() != allonesval) return false;
            return true;
        }

        // Given a known double precis input, look for double precision compares of the form
        //   a == -1,  a != -1
        //
        // We look for
        //     hi & lo == -1
        public bool applyRule(SplitVarnode i, PcodeOp op, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;
            if (!verify(@in.getHi(), @in.getLo(), op))
                return false;

            SplitVarnode in2(@in.getSize(), Globals.calc_mask(@in.getSize()));    // Create the -1 value
            if (!SplitVarnode::prepareBoolOp(@in, in2, compareop)) return false;
            SplitVarnode::replaceBoolOp(data, compareop, @in, in2, compareop->code());
            return true;
        }
    }
}
