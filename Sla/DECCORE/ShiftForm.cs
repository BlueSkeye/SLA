using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class ShiftForm
    {
        private SplitVarnode @in;
        private OpCode opc;           // Basic operation
        private PcodeOp loshift;
        private PcodeOp midshift;
        private PcodeOp hishift;
        private PcodeOp orop;
        private Varnode lo;
        private Varnode hi;
        private Varnode midlo;
        private Varnode midhi;
        private Varnode salo;
        private Varnode sahi;
        private Varnode samid;
        private Varnode reslo;
        private Varnode reshi;
        private SplitVarnode @out;
        private PcodeOp existop;

        private bool verifyShiftAmount()
        { // Make sure all the shift amount varnodes are consistent
            if (!salo->isConstant()) return false;
            if (!samid->isConstant()) return false;
            if (!sahi->isConstant()) return false;
            uintb val = salo->getOffset();
            if (val != sahi->getOffset()) return false;
            if (val >= 8 * lo->getSize()) return false; // If shift amount is so big, we would not use this form
            val = 8 * lo->getSize() - val;
            if (samid->getOffset() != val) return false;
            return true;
        }

        private bool mapLeft()
        { // Assume reshi, reslo are filled in, fill in other ops and varnodes
            if (!reslo->isWritten()) return false;
            if (!reshi->isWritten()) return false;
            loshift = reslo->getDef();
            opc = loshift->code();
            if (opc != CPUI_INT_LEFT) return false;
            orop = reshi->getDef();
            if ((orop->code() != CPUI_INT_OR) && (orop->code() != CPUI_INT_XOR) && (orop->code() != CPUI_INT_ADD))
                return false;
            midlo = orop->getIn(0);
            midhi = orop->getIn(1);
            if (!midlo->isWritten()) return false;
            if (!midhi->isWritten()) return false;
            if (midhi->getDef()->code() != CPUI_INT_LEFT)
            {
                Varnode* tmpvn = midhi;
                midhi = midlo;
                midlo = tmpvn;
            }
            midshift = midlo->getDef();
            if (midshift->code() != CPUI_INT_RIGHT) return false;   // Must be unsigned RIGHT
            hishift = midhi->getDef();
            if (hishift->code() != CPUI_INT_LEFT) return false;

            if (lo != loshift->getIn(0)) return false;
            if (hi != hishift->getIn(0)) return false;
            if (lo != midshift->getIn(0)) return false;
            salo = loshift->getIn(1);
            sahi = hishift->getIn(1);
            samid = midshift->getIn(1);
            return true;
        }

        private bool mapRight()
        { // Assume reshi, reslo are filled in, fill in other ops and varnodes
            if (!reslo->isWritten()) return false;
            if (!reshi->isWritten()) return false;
            hishift = reshi->getDef();
            opc = hishift->code();
            if ((opc != CPUI_INT_RIGHT) && (opc != CPUI_INT_SRIGHT)) return false;
            orop = reslo->getDef();
            if ((orop->code() != CPUI_INT_OR) && (orop->code() != CPUI_INT_XOR) && (orop->code() != CPUI_INT_ADD))
                return false;
            midlo = orop->getIn(0);
            midhi = orop->getIn(1);
            if (!midlo->isWritten()) return false;
            if (!midhi->isWritten()) return false;
            if (midlo->getDef()->code() != CPUI_INT_RIGHT)
            { // Must be unsigned RIGHT
                Varnode* tmpvn = midhi;
                midhi = midlo;
                midlo = tmpvn;
            }
            midshift = midhi->getDef();
            if (midshift->code() != CPUI_INT_LEFT) return false;
            loshift = midlo->getDef();
            if (loshift->code() != CPUI_INT_RIGHT) return false; // Must be unsigned RIGHT

            if (lo != loshift->getIn(0)) return false;
            if (hi != hishift->getIn(0)) return false;
            if (hi != midshift->getIn(0)) return false;
            salo = loshift->getIn(1);
            sahi = hishift->getIn(1);
            samid = midshift->getIn(1);
            return true;
        }

        public bool verifyLeft(Varnode h, Varnode l, PcodeOp loop)
        {
            hi = h;
            lo = l;

            loshift = loop;
            reslo = loshift->getOut();

            list<PcodeOp*>::const_iterator iter, enditer;
            iter = hi->beginDescend();
            enditer = hi->endDescend();
            while (iter != enditer)
            {
                hishift = *iter;
                ++iter;
                if (hishift->code() != CPUI_INT_LEFT) continue;
                Varnode* outvn = hishift->getOut();
                list<PcodeOp*>::const_iterator iter2, enditer2;
                iter2 = outvn->beginDescend();
                enditer2 = outvn->endDescend();
                while (iter2 != enditer2)
                {
                    midshift = *iter2;
                    ++iter2;
                    Varnode* tmpvn = midshift->getOut();
                    if (tmpvn == (Varnode*)0) continue;
                    reshi = tmpvn;
                    if (!mapLeft()) continue;
                    if (!verifyShiftAmount()) continue;
                    return true;
                }
            }
            return false;
        }

        public bool verifyRight(Varnode h, Varnode l, PcodeOp hiop)
        {
            hi = h;
            lo = l;
            hishift = hiop;
            reshi = hiop->getOut();

            list<PcodeOp*>::const_iterator iter, enditer;
            iter = lo->beginDescend();
            enditer = lo->endDescend();
            while (iter != enditer)
            {
                loshift = *iter;
                ++iter;
                if (loshift->code() != CPUI_INT_RIGHT) continue;
                Varnode* outvn = loshift->getOut();
                list<PcodeOp*>::const_iterator iter2, enditer2;
                iter2 = outvn->beginDescend();
                enditer2 = outvn->endDescend();
                while (iter2 != enditer2)
                {
                    midshift = *iter2;
                    ++iter2;
                    Varnode* tmpvn = midshift->getOut();
                    if (tmpvn == (Varnode*)0) continue;
                    reslo = tmpvn;
                    if (!mapRight()) continue;
                    if (!verifyShiftAmount()) continue;
                    return true;
                }
            }
            return false;
        }

        public bool applyRuleLeft(SplitVarnode i, PcodeOp loop, bool workishi, Funcdata data)
        {
            if (workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;

            if (!verifyLeft(@in.getHi(), @in.getLo(), loop))
                return false;

            @out.initPartial(@in.getSize(), reslo, reshi);
            existop = SplitVarnode::prepareShiftOp(@out, @in);
            if (existop == (PcodeOp*)0)
                return false;
            SplitVarnode::createShiftOp(data, @out, @in, salo, existop, opc);
            return true;
        }

        public bool applyRuleRight(SplitVarnode i, PcodeOp hiop, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;

            if (!verifyRight(@in.getHi(), @in.getLo(), hiop))
                return false;

            @out.initPartial(@in.getSize(), reslo, reshi);
            existop = SplitVarnode::prepareShiftOp(@out, @in);
            if (existop == (PcodeOp*)0)
                return false;
            SplitVarnode::createShiftOp(data, @out, @in, salo, existop, opc);
            return true;
        }
    }
}
