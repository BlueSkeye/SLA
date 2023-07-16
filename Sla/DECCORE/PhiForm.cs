using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    internal class PhiForm
    {
        private SplitVarnode @in;
        private SplitVarnode outvn;
        private int4 inslot;
        private Varnode hibase;
        private Varnode lobase;
        private BlockBasic blbase;
        private PcodeOp lophi;
        private PcodeOp hiphi;
        private PcodeOp existop;

        // Given a known double precis coming together with two other pieces (via phi-nodes)
        // Create a double precision phi-node
        public bool verify(Varnode h, Varnode l, PcodeOp hphi)
        {
            hibase = h;
            lobase = l;
            hiphi = hphi;

            inslot = hiphi->getSlot(hibase);

            if (hiphi->getOut()->hasNoDescend()) return false;
            blbase = hiphi->getParent();

            list<PcodeOp*>::const_iterator iter, enditer;
            iter = lobase->beginDescend();
            enditer = lobase->endDescend();
            while (iter != enditer)
            {
                lophi = *iter;
                ++iter;
                if (lophi->code() != CPUI_MULTIEQUAL) continue;
                if (lophi->getParent() != blbase) continue;
                if (lophi->getIn(inslot) != lobase) continue;
                return true;
            }
            return false;
        }

        public bool applyRule(SplitVarnode i, PcodeOp hphi, bool workishi, Funcdata data)
        {
            if (!workishi) return false;
            if (!i.hasBothPieces()) return false;
            @in = i;

            if (!verify(@in.getHi(), @in.getLo(), hphi))
                return false;

            int4 numin = hiphi->numInput();
            vector<SplitVarnode> inlist;
            for (int4 j = 0; j < numin; ++j)
            {
                Varnode* vhi = hiphi->getIn(j);
                Varnode* vlo = lophi->getIn(j);
                inlist.push_back(SplitVarnode(vlo, vhi));
            }
            outvn.initPartial(@in.getSize(), lophi->getOut(), hiphi->getOut());
            existop = SplitVarnode::preparePhiOp(outvn, inlist);
            if (existop != (PcodeOp*)0)
            {
                SplitVarnode::createPhiOp(data, outvn, inlist, existop);
                return true;
            }
            return false;
        }
    }
}
