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
    internal class RuleEqual2Zero : Rule
    {
        public RuleEqual2Zero(string g)
            : base(g, 0, "equal2zero")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleEqual2Zero(getGroup());
        }

        /// \class RuleEqual2Zero
        /// \brief Simplify INT_EQUAL applied to 0: `0 == V + W * -1  =>  V == W  or  0 == V + c  =>  V == -c`
        ///
        /// The Rule also applies to INT_NOTEQUAL comparisons.
        public override void getOpList(List<uint> oplist)
        {
            uint list[] = { CPUI_INT_EQUAL, CPUI_INT_NOTEQUAL };
            oplist.insert(oplist.end(), list, list + 2);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn;
            Varnode vn2;
            Varnode addvn;
            Varnode posvn;
            Varnode negvn;
            Varnode unnegvn;
            PcodeOp addop;

            vn = op.getIn(0);
            if ((vn.isConstant()) && (vn.getOffset() == 0))
                addvn = op.getIn(1);
            else
            {
                addvn = vn;
                vn = op.getIn(1);
                if ((!vn.isConstant()) || (vn.getOffset() != 0))
                    return 0;
            }
            for (list<PcodeOp*>::const_iterator iter = addvn.beginDescend(); iter != addvn.endDescend(); ++iter)
            {
                // make sure the sum is only used in comparisons
                PcodeOp* boolop = *iter;
                if (!boolop.isBoolOutput()) return 0;
            }
            //  if (addvn.lone_descendant() != op) return 0;
            addop = addvn.getDef();
            if (addop == (PcodeOp)null) return 0;
            if (addop.code() != CPUI_INT_ADD) return 0;
            vn = addop.getIn(0);
            vn2 = addop.getIn(1);
            if (vn2.isConstant())
            {
                Address val = new Address(vn2.getSpace(), uintb_negate(vn2.getOffset()-1,vn2.getSize()));
                unnegvn = data.newVarnode(vn2.getSize(), val);
                unnegvn.copySymbolIfValid(vn2);    // Propagate any markup
                posvn = vn;
            }
            else
            {
                if ((vn.isWritten()) && (vn.getDef().code() == CPUI_INT_MULT))
                {
                    negvn = vn;
                    posvn = vn2;
                }
                else if ((vn2.isWritten()) && (vn2.getDef().code() == CPUI_INT_MULT))
                {
                    negvn = vn2;
                    posvn = vn;
                }
                else
                    return 0;
                ulong multiplier;
                if (!negvn.getDef().getIn(1).isConstant()) return 0;
                unnegvn = negvn.getDef().getIn(0);
                multiplier = negvn.getDef().getIn(1).getOffset();
                if (multiplier != Globals.calc_mask(unnegvn.getSize())) return 0;
            }
            if (!posvn.isHeritageKnown()) return 0;
            if (!unnegvn.isHeritageKnown()) return 0;

            data.opSetInput(op, posvn, 0);
            data.opSetInput(op, unnegvn, 1);
            return 1;
        }
    }
}
