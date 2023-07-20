﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    internal class IndirectForm
    {
        private SplitVarnode @in;
        private SplitVarnode outvn;
        private Varnode lo;
        private Varnode hi;
        private Varnode reslo;
        private Varnode reshi;
        // Single op affecting both lo and hi
        private PcodeOp affector;
        private PcodeOp indhi;
        // Two partial CPUI_INDIRECT ops
        private Varnode indlo;

        public bool verify(Varnode h, Varnode l, PcodeOp ihi)
        {  // Verify the basic double precision indirect form and fill out the pieces
            hi = h;
            lo = l;
            indhi = ind;
            if (indhi->getIn(1)->getSpace()->getType() != IPTR_IOP) return false;
            affector = PcodeOp::getOpFromConst(indhi->getIn(1)->getAddr());
            if (affector->isDead()) return false;
            reshi = indhi->getOut();
            if (reshi->getSpace()->getType() == IPTR_INTERNAL) return false;        // Indirect must not be through a temporary

            list<PcodeOp*>::const_iterator iter, enditer;
            iter = lo->beginDescend();
            enditer = lo->endDescend();
            while (iter != enditer)
            {
                indlo = *iter;
                ++iter;
                if (indlo->code() != CPUI_INDIRECT) continue;
                if (indlo->getIn(1)->getSpace()->getType() != IPTR_IOP) continue;
                if (affector != PcodeOp::getOpFromConst(indlo->getIn(1)->getAddr())) continue;  // hi and lo must be affected by same op
                reslo = indlo->getOut();
                if (reslo->getSpace()->getType() == IPTR_INTERNAL) return false;        // Indirect must not be through a temporary
                if (reslo->isAddrTied() || reshi->isAddrTied())
                {
                    Address addr;
                    // If one piece is address tied, the other must be as well, and they must fit together as contiguous whole
                    if (!SplitVarnode::isAddrTiedContiguous(reslo, reshi, addr))
                        return false;
                }
                return true;
            }
            return false;
        }

        public bool applyRule(SplitVarnode i, PcodeOp ind, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;
            if (!verify(@in.getHi(), @in.getLo(), ind))
                return false;

            outvn.initPartial(@in.getSize(), reslo, reshi);

            if (!SplitVarnode::prepareIndirectOp(@in, affector))
                return false;
            SplitVarnode::replaceIndirectOp(data, outvn, @in, affector);
            return true;
        }
    }
}