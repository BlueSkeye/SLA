using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
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
        // Two partial OpCode.CPUI_INDIRECT ops
        private Varnode indlo;

        public bool verify(Varnode h, Varnode l, PcodeOp ihi)
        {
            // Verify the basic double precision indirect form and fill out the pieces
            hi = h;
            lo = l;
            indhi = ihi;
            if (indhi.getIn(1).getSpace().getType() != spacetype.IPTR_IOP)
                return false;
            affector = PcodeOp.getOpFromConst(indhi.getIn(1).getAddr());
            if (affector.isDead())
                return false;
            reshi = indhi.getOut();
            if (reshi.getSpace().getType() == spacetype.IPTR_INTERNAL)
                // Indirect must not be through a temporary
                return false;

            IEnumerator<PcodeOp> iter = lo.beginDescend();
            while (iter.MoveNext()) {
                indlo = iter.Current;
                if (indlo.code() != OpCode.CPUI_INDIRECT)
                    continue;
                if (indlo.getIn(1).getSpace().getType() != spacetype.IPTR_IOP)
                    continue;
                if (affector != PcodeOp.getOpFromConst(indlo.getIn(1).getAddr()))
                    // hi and lo must be affected by same op
                    continue;
                reslo = indlo.getOut();
                if (reslo.getSpace().getType() == spacetype.IPTR_INTERNAL)
                    // Indirect must not be through a temporary
                    return false;
                if (reslo.isAddrTied() || reshi.isAddrTied()) {
                    Address addr = new Address();
                    // If one piece is address tied, the other must be as well, and they must
                    // fit together as contiguous whole
                    if (!SplitVarnode.isAddrTiedContiguous(reslo, reshi, addr))
                        return false;
                }
                return true;
            }
            return false;
        }

        public bool applyRule(SplitVarnode i, PcodeOp ind, bool workishi, Funcdata data)
        {
            if (!workishi) {
                return false;
            }
            if (!i.hasBothPieces()) {
                return false;
            }
            @in = i;
            if (!verify(@in.getHi(), @in.getLo(), ind)) {
                return false;
            }

            outvn.initPartial(@in.getSize(), reslo, reshi);

            if (!SplitVarnode.prepareIndirectOp(@in, affector)) {
                return false;
            }
            SplitVarnode.replaceIndirectOp(data, outvn, @in, affector);
            return true;
        }
    }
}
