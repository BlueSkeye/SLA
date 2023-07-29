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
    internal class RuleEqual2Constant : Rule
    {
        public RuleEqual2Constant(string g)
            : base(g, 0, "equal2constant")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleEqual2Constant(getGroup());
        }

        /// \class RuleEqual2Constant
        /// \brief Simplify INT_EQUAL applied to arithmetic expressions
        ///
        /// Forms include:
        ///  - `V * -1 == c  =>  V == -c`
        ///  - `V + c == d  =>  V == (d-c)`
        ///  - `~V == c     =>  V == ~c`
        public override void getOpList(List<uint> oplist)
        {
            uint list[] = { CPUI_INT_EQUAL, CPUI_INT_NOTEQUAL };
            oplist.insert(oplist.end(), list, list + 2);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* cvn = op.getIn(1);
            if (!cvn.isConstant()) return 0;

            Varnode* lhs = op.getIn(0);
            if (!lhs.isWritten()) return 0;
            PcodeOp* leftop = lhs.getDef();
            Varnode* a;
            ulong newconst;
            OpCode opc = leftop.code();
            if (opc == CPUI_INT_ADD)
            {
                Varnode* otherconst = leftop.getIn(1);
                if (!otherconst.isConstant()) return 0;
                newconst = cvn.getOffset() - otherconst.getOffset();
                newconst &= calc_mask(cvn.getSize());
            }
            else if (opc == CPUI_INT_MULT)
            {
                Varnode* otherconst = leftop.getIn(1);
                if (!otherconst.isConstant()) return 0;
                // The only multiply we transform, is multiply by -1
                if (otherconst.getOffset() != calc_mask(otherconst.getSize())) return 0;
                newconst = cvn.getOffset();
                newconst = (-newconst) & calc_mask(otherconst.getSize());
            }
            else if (opc == CPUI_INT_NEGATE)
            {
                newconst = cvn.getOffset();
                newconst = (~newconst) & calc_mask(lhs.getSize());
            }
            else
                return 0;

            a = leftop.getIn(0);
            if (a.isFree()) return 0;

            // Make sure the transformed form of a is only used
            // in comparisons of similar form
            list<PcodeOp*>::const_iterator iter;
            for (iter = lhs.beginDescend(); iter != lhs.endDescend(); ++iter)
            {
                PcodeOp* dop = *iter;
                if (dop == op) continue;
                if ((dop.code() != CPUI_INT_EQUAL) && (dop.code() != CPUI_INT_NOTEQUAL))
                    return 0;
                if (!dop.getIn(1).isConstant()) return 0;
            }

            data.opSetInput(op, a, 0);
            data.opSetInput(op, data.newConstant(a.getSize(), newconst), 1);
            return 1;
        }
    }
}
