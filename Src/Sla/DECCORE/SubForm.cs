using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class SubForm
    {
        private SplitVarnode @in;
        private Varnode hi1;
        private Varnode hi2;
        private Varnode lo1;
        private Varnode lo2;
        private Varnode reshi;
        private Varnode reslo;
        private PcodeOp zextop;
        private PcodeOp lessop;
        private PcodeOp negop;
        private PcodeOp loadd;
        private PcodeOp add2;
        private Varnode hineg1;
        private Varnode hineg2;
        private Varnode hizext1;
        private Varnode hizext2;
        private int slot1;
        private PcodeOp existop;
        private SplitVarnode indoub;
        private SplitVarnode outdoub;

        // Given a known double precision input, look for a double precision subtraction,
        // recovering the other double input and the double output
        //
        // Assume hi1, lo1 is a known double precision pair, we look for
        //   reshi = hi1 + -hi2 + - zext(lo1 < lo2)
        //   reslo = lo1 + -lo2
        public bool verify(Varnode h, Varnode l, PcodeOp op)
        {
            list<PcodeOp*>::const_iterator iter2, enditer2;
            hi1 = h;
            lo1 = l;
            slot1 = op.getSlot(hi1);
            for (int i = 0; i < 2; ++i)
            {
                if (i == 0)
                {       // Assume we have to descend one more add
                    add2 = op.getOut().loneDescend();
                    if (add2 == (PcodeOp)null) continue;
                    if (add2.code() != OpCode.CPUI_INT_ADD) continue;
                    reshi = add2.getOut();
                    hineg1 = op.getIn(1 - slot1);
                    hineg2 = add2.getIn(1 - add2.getSlot(op.getOut()));
                }
                else
                {
                    Varnode* tmpvn = op.getIn(1 - slot1);
                    if (!tmpvn.isWritten()) continue;
                    add2 = tmpvn.getDef();
                    if (add2.code() != OpCode.CPUI_INT_ADD) continue;
                    reshi = op.getOut();
                    hineg1 = add2.getIn(0);
                    hineg2 = add2.getIn(1);
                }
                if (!hineg1.isWritten()) continue;
                if (!hineg2.isWritten()) continue;
                if (!SplitVarnode::verifyMultNegOne(hineg1.getDef())) continue;
                if (!SplitVarnode::verifyMultNegOne(hineg2.getDef())) continue;
                hizext1 = hineg1.getDef().getIn(0);
                hizext2 = hineg2.getDef().getIn(0);
                for (int j = 0; j < 2; ++j)
                {
                    if (j == 0)
                    {
                        if (!hizext1.isWritten()) continue;
                        zextop = hizext1.getDef();
                        hi2 = hizext2;
                    }
                    else
                    {
                        if (!hizext2.isWritten()) continue;
                        zextop = hizext2.getDef();
                        hi2 = hizext1;
                    }
                    if (zextop.code() != OpCode.CPUI_INT_ZEXT) continue;
                    if (!zextop.getIn(0).isWritten()) continue;
                    lessop = zextop.getIn(0).getDef();
                    if (lessop.code() != OpCode.CPUI_INT_LESS) continue;
                    if (lessop.getIn(0) != lo1) continue;
                    lo2 = lessop.getIn(1);
                    iter2 = lo1.beginDescend();
                    enditer2 = lo1.endDescend();
                    while (iter2 != enditer2)
                    {
                        loadd = *iter2;
                        ++iter2;
                        if (loadd.code() != OpCode.CPUI_INT_ADD) continue;
                        Varnode* tmpvn = loadd.getIn(1 - loadd.getSlot(lo1));
                        if (!tmpvn.isWritten()) continue;
                        negop = tmpvn.getDef();
                        if (!SplitVarnode::verifyMultNegOne(negop)) continue;
                        if (negop.getIn(0) != lo2) continue;
                        reslo = loadd.getOut();
                        return true;
                    }
                }
            }
            return false;
        }

        private bool applyRule(SplitVarnode i, PcodeOp op, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;

            if (!verify(@@in.getHi(), @@in.getLo(), op))
                return false;

            indoub.initPartial(@@in.getSize(), lo2, hi2);
            outdoub.initPartial(@@in.getSize(), reslo, reshi);
            existop = SplitVarnode::prepareBinaryOp(outdoub, @in, indoub);
            if (existop == (PcodeOp)null)
                return false;
            SplitVarnode::createBinaryOp(data, outdoub, @in, indoub, existop, OpCode.CPUI_INT_SUB);
            return true;
        }
    }
}
