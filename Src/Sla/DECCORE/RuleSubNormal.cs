using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubNormal : Rule
    {
        public RuleSubNormal(string g)
            : base(g, 0, "subnormal")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubNormal(getGroup());
        }

        /// \class RuleSubNormal
        /// \brief Pull-back SUBPIECE through INT_RIGHT and INT_SRIGHT
        ///
        /// The form looks like:
        ///  - `sub( V>>n ,c )  =>  sub( V, c+k/8 ) >> (n-k)  where k = (n/8)*8`  or
        ///  - `sub( V>>n, c )  =>  ext( sub( V, c+k/8 ) )  if n is big`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* shiftout = op.getIn(0);
            if (!shiftout.isWritten()) return 0;
            PcodeOp* shiftop = shiftout.getDef();
            OpCode opc = shiftop.code();
            if ((opc != CPUI_INT_RIGHT) && (opc != CPUI_INT_SRIGHT))
                return 0;
            if (!shiftop.getIn(1).isConstant()) return 0;
            Varnode* a = shiftop.getIn(0);
            if (a.isFree()) return 0;
            int n = shiftop.getIn(1).getOffset();
            int c = op.getIn(1).getOffset();
            int k = (n / 8);
            int insize = a.getSize();
            int outsize = op.getOut().getSize();

            // Total shift + outsize must be greater equal to size of input
            if ((n + 8 * c + 8 * outsize < 8 * insize) && (n != k * 8)) return 0;

            // If totalcut + remain > original input
            if (k + c + outsize > insize)
            {
                int truncSize = insize - c - k;
                if (n == k * 8 && truncSize > 0 && popcount(truncSize) == 1)
                {
                    // We need an additional extension
                    c += k;
                    PcodeOp* newop = data.newOp(2, op.getAddr());
                    opc = (opc == CPUI_INT_SRIGHT) ? CPUI_INT_SEXT : CPUI_INT_ZEXT;
                    data.opSetOpcode(newop, CPUI_SUBPIECE);
                    data.newUniqueOut(truncSize, newop);
                    data.opSetInput(newop, a, 0);
                    data.opSetInput(newop, data.newConstant(4, c), 1);
                    data.opInsertBefore(newop, op);

                    data.opSetInput(op, newop.getOut(), 0);
                    data.opRemoveInput(op, 1);
                    data.opSetOpcode(op, opc);
                    return 1;
                }
                else
                    k = insize - c - outsize; // Or we can shrink the cut
            }

            // if n == k*8, then a shift is unnecessary
            c += k;
            n -= k * 8;
            if (n == 0)
            {           // Extra shift is unnecessary
                data.opSetInput(op, a, 0);
                data.opSetInput(op, data.newConstant(4, c), 1);
                return 1;
            }
            else if (n >= outsize * 8)
            {
                n = outsize * 8;        // Can only shift so far
                if (opc == CPUI_INT_SRIGHT)
                    n -= 1;
            }

            PcodeOp* newop = data.newOp(2, op.getAddr());
            data.opSetOpcode(newop, CPUI_SUBPIECE);
            data.newUniqueOut(outsize, newop);
            data.opSetInput(newop, a, 0);
            data.opSetInput(newop, data.newConstant(4, c), 1);
            data.opInsertBefore(newop, op);

            data.opSetInput(op, newop.getOut(), 0);
            data.opSetInput(op, data.newConstant(4, n), 1);
            data.opSetOpcode(op, opc);
            return 1;
        }
    }
}
