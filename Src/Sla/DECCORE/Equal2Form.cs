using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class Equal2Form
    {
        private SplitVarnode @in;
        private Varnode hi1;
        private Varnode hi2;
        private Varnode lo1;
        private Varnode lo2;
        private PcodeOp equalop;
        private PcodeOp orop;
        private PcodeOp hixor;
        private PcodeOp loxor;
        private int orhislot;
        private int xorhislot;
        private SplitVarnode param2;

        private bool checkLoForm()
        { // Assuming we have equal <- or <- xor <- hi1, verify if we have the full equal form
            Varnode* orvnin = orop.getIn(1 - orhislot);
            if (orvnin == lo1)
            {       // lo2 is an implied 0
                loxor = (PcodeOp)null;
                lo2 = (Varnode)null;
                return true;
            }
            if (!orvnin.isWritten()) return false;
            loxor = orvnin.getDef();
            if (loxor.code() != OpCode.CPUI_INT_XOR) return false;
            if (loxor.getIn(0) == lo1)
            {
                lo2 = loxor.getIn(1);
                return true;
            }
            else if (loxor.getIn(1) == lo1)
            {
                lo2 = loxor.getIn(0);
                return true;
            }
            return false;
        }

        private bool fillOutFromOr(Funcdata data)
        {
            // We have filled in either or <- xor <- hi1,  OR,  or <- hi1
            // Now try to fill in the rest of the form
            Varnode outvn = orop.getOut();
            IEnumerator<PcodeOp> iter = outvn.beginDescend();
            while (iter.MoveNext()) {
                equalop = iter.Current;
                if (   (equalop.code() != OpCode.CPUI_INT_EQUAL)
                    && (equalop.code() != OpCode.CPUI_INT_NOTEQUAL))
                {
                    continue;
                }
                if (!equalop.getIn(1).isConstant()) continue;
                if (equalop.getIn(1).getOffset() != 0) continue;

                if (!checkLoForm()) continue;
                if (!replace(data)) continue;
                return true;
            }
            return false;
        }

        private bool replace(Funcdata data)
        {
            if ((hi2 == (Varnode)null) && (lo2 == (Varnode)null))
            {
                param2.initPartial(@in.getSize(), 0); // Double precis zero constant
                return SplitVarnode.prepareBoolOp(in, param2, equalop);
            }
            if ((hi2 == (Varnode)null) && (lo2.isConstant()))
            {
                param2.initPartial(@in.getSize(), lo2.getOffset());
                return SplitVarnode.prepareBoolOp(in, param2, equalop);
            }
            if ((lo2 == (Varnode)null) && (hi2.isConstant()))
            {
                param2.initPartial(@in.getSize(), hi2.getOffset() << 8 * lo1.getSize());
                return SplitVarnode.prepareBoolOp(in, param2, equalop);
            }
            if (lo2 == (Varnode)null)
            {
                // Equal to a zero extended and shifted var
                return false;
            }
            if (hi2 == (Varnode)null)
            {
                // Equal to a zero extended var
                return false;
            }
            if (hi2.isConstant() && lo2.isConstant())
            {
                ulong val = hi2.getOffset();
                val <<= 8 * lo1.getSize();
                val |= lo2.getOffset();
                param2.initPartial(@in.getSize(), val);
                return SplitVarnode.prepareBoolOp(in, param2, equalop);
            }
            if (hi2.isConstant() || lo2.isConstant())
            {
                // Some kind of mixed form
                return false;
            }
            param2.initPartial(@in.getSize(), lo2, hi2);
            return SplitVarnode.prepareBoolOp(in, param2, equalop);
        }

        // Given a known double precis input, look for double precision compares of the form
        //   a == b,  a != b
        //
        // We look for
        //     res = ((hi1 ^ hi2) | (lo1 ^ lo2) == 0)
        //  where hi2 or lo2 may be zero, and optimized out
        public bool applyRule(SplitVarnode i, PcodeOp op, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;
            hi1 = @in.getHi();
            lo1 = @in.getLo();

            if (op.code() == OpCode.CPUI_INT_OR) {
                orop = op;
                orhislot = op.getSlot(hi1);
                hixor = (PcodeOp)null;
                hi2 = (Varnode)null;
                if (fillOutFromOr(data)) {
                    SplitVarnode.replaceBoolOp(data, equalop, in, param2, equalop.code());
                    return true;
                }
            }
            else {
                // We see an XOR
                hixor = op;
                xorhislot = hixor.getSlot(hi1);
                hi2 = hixor.getIn(1 - xorhislot);
                Varnode vn = op.getOut();
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                while (iter.MoveNext()) {
                    orop = iter.Current;
                    if (orop.code() != OpCode.CPUI_INT_OR) continue;
                    orhislot = orop.getSlot(vn);
                    if (fillOutFromOr(data)) {
                        SplitVarnode.replaceBoolOp(data, equalop, @in, param2, equalop.code());
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
