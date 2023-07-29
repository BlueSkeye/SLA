using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleTestSign : Rule
    {
        /// \brief Find INT_EQUAL or INT_NOTEQUAL taking the sign bit as input
        ///
        /// Trace the given sign-bit varnode to any comparison operations and pass them
        /// back in the given array.
        /// \param vn is the given sign-bit varnode
        /// \param res is the array for holding the comparison op results
        private void findComparisons(Varnode vn, List<PcodeOp> res)
        {
            list<PcodeOp*>::const_iterator iter1;
            iter1 = vn.beginDescend();
            while (iter1 != vn.endDescend())
            {
                PcodeOp* op = *iter1;
                ++iter1;
                OpCode opc = op.code();
                if (opc == CPUI_INT_EQUAL || opc == CPUI_INT_NOTEQUAL)
                {
                    if (op.getIn(1).isConstant())
                        res.push_back(op);
                }
            }
        }

        public RuleTestSign(string g)
            : base(g, 0, "testsign")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleTestSign(getGroup());
        }

        /// \class RuleTestSign
        /// \brief Convert sign-bit test to signed comparison:  `(V s>> 0x1f) != 0   =>  V s< 0`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            ulong val;
            Varnode* constVn = op.getIn(1);
            if (!constVn.isConstant()) return 0;
            val = constVn.getOffset();
            Varnode* inVn = op.getIn(0);
            if (val != 8 * inVn.getSize() - 1) return 0;
            Varnode* outVn = op.getOut();

            if (inVn.isFree()) return 0;
            List<PcodeOp*> compareOps;
            findComparisons(outVn, compareOps);
            int resultCode = 0;
            for (int i = 0; i < compareOps.size(); ++i)
            {
                PcodeOp* compareOp = compareOps[i];
                Varnode* compVn = compareOp.getIn(0);
                int compSize = compVn.getSize();

                ulong offset = compareOp.getIn(1).getOffset();
                int sgn;
                if (offset == 0)
                    sgn = 1;
                else if (offset == calc_mask(compSize))
                    sgn = -1;
                else
                    continue;
                if (compareOp.code() == CPUI_INT_NOTEQUAL)
                    sgn = -sgn; // Complement the domain

                Varnode* zeroVn = data.newConstant(inVn.getSize(), 0);
                if (sgn == 1)
                {
                    data.opSetInput(compareOp, inVn, 1);
                    data.opSetInput(compareOp, zeroVn, 0);
                    data.opSetOpcode(compareOp, CPUI_INT_SLESSEQUAL);
                }
                else
                {
                    data.opSetInput(compareOp, inVn, 0);
                    data.opSetInput(compareOp, zeroVn, 1);
                    data.opSetOpcode(compareOp, CPUI_INT_SLESS);
                }
                resultCode = 1;
            }
            return resultCode;
        }
    }
}
