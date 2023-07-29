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
    internal class RuleLzcountShiftBool : Rule
    {
        public RuleLzcountShiftBool(string g)
            : base(g, 0, "lzcountshiftbool")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleLzcountShiftBool(getGroup());
        }

        /// \class RuleLzcountShiftBool
        /// \brief Simplify equality checks that use lzcount:  `lzcount(X) >> c  =>  X == 0` if X is 2^c bits wide
        ///
        /// Some compilers check if a value is equal to zero by checking the most
        /// significant bit in lzcount; for instance on a 32-bit system,
        /// the result of lzcount on zero would have the 5th bit set.
        ///  - `lzcount(a ^ 3) >> 5  =>  a ^ 3 == 0  =>  a == 3` (by RuleXorCollapse)
        ///  - `lzcount(a - 3) >> 5  =>  a - 3 == 0  =>  a == 3` (by RuleEqual2Zero)
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_LZCOUNT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* outVn = op.getOut();
            list<PcodeOp*>::const_iterator iter, iter2;
            ulong max_return = 8 * op.getIn(0).getSize();
            if (popcount(max_return) != 1)
            {
                // This rule only makes sense with sizes that are powers of 2; if the maximum value
                // returned by lzcount was, say, 24, then both 16 >> 4 and 24 >> 4
                // are 1, and thus the check does not make sense.  (Such processors couldn't
                // use lzcount for checking equality in any case.)
                return 0;
            }

            for (iter = outVn.beginDescend(); iter != outVn.endDescend(); ++iter)
            {
                PcodeOp* baseOp = *iter;
                if (baseOp.code() != CPUI_INT_RIGHT && baseOp.code() != CPUI_INT_SRIGHT) continue;
                Varnode* vn1 = baseOp.getIn(1);
                if (!vn1.isConstant()) continue;
                ulong shift = vn1.getOffset();
                if ((max_return >> shift) == 1)
                {
                    // Becomes a comparison with zero
                    PcodeOp* newOp = data.newOp(2, baseOp.getAddr());
                    data.opSetOpcode(newOp, CPUI_INT_EQUAL);
                    Varnode* b = data.newConstant(op.getIn(0).getSize(), 0);
                    data.opSetInput(newOp, op.getIn(0), 0);
                    data.opSetInput(newOp, b, 1);

                    // CPUI_INT_EQUAL must produce a 1-byte boolean result
                    Varnode* eqResVn = data.newUniqueOut(1, newOp);

                    data.opInsertBefore(newOp, baseOp);

                    // Because the old output had size op.getIn(0).getSize(),
                    // we have to guarantee that a Varnode of this size gets outputted
                    // to the descending PcodeOps. This is handled here with CPUI_INT_ZEXT.
                    data.opRemoveInput(baseOp, 1);
                    if (baseOp.getOut().getSize() == 1)
                        data.opSetOpcode(baseOp, CPUI_COPY);
                    else
                        data.opSetOpcode(baseOp, CPUI_INT_ZEXT);
                    data.opSetInput(baseOp, eqResVn, 0);
                    return 1;
                }
            }
            return 0;
        }
    }
}
