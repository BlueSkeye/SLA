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
    internal class RuleSignForm2 : Rule
    {
        public RuleSignForm2(string g)
            : base(g, 0, "signform2")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSignForm2(getGroup());
        }

        /// \class RuleSignForm2
        /// \brief Normalize sign extraction:  `sub(sext(V) * small,c) s>> 31  =>  V s>> 31`
        ///
        /// V and small must be small enough so that there is no overflow in the INT_MULT.
        /// The SUBPIECE must be extracting the high part of the INT_MULT.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_SRIGHT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constVn = op.getIn(1);
            if (!constVn.isConstant()) return 0;
            Varnode* inVn = op.getIn(0);
            int4 sizeout = inVn.getSize();
            if ((int4)constVn.getOffset() != sizeout * 8 - 1) return 0;
            if (!inVn.isWritten()) return 0;
            PcodeOp* subOp = inVn.getDef();
            if (subOp.code() != CPUI_SUBPIECE) return 0;
            int4 c = subOp.getIn(1).getOffset();
            Varnode* multOut = subOp.getIn(0);
            int4 multSize = multOut.getSize();
            if (c + sizeout != multSize) return 0;  // Must be extracting high part
            if (!multOut.isWritten()) return 0;
            PcodeOp* multOp = multOut.getDef();
            if (multOp.code() != CPUI_INT_MULT) return 0;
            int4 slot;
            PcodeOp* sextOp;
            for (slot = 0; slot < 2; ++slot)
            {           // Search for the INT_SEXT
                Varnode* vn = multOp.getIn(slot);
                if (!vn.isWritten()) continue;
                sextOp = vn.getDef();
                if (sextOp.code() == CPUI_INT_SEXT) break;
            }
            if (slot > 1) return 0;
            Varnode* a = sextOp.getIn(0);
            if (a.isFree() || a.getSize() != sizeout) return 0;
            Varnode* otherVn = multOp.getIn(1 - slot);
            // otherVn must be a positive integer and small enough so the INT_MULT can't overflow into the sign-bit
            if (otherVn.isConstant())
            {
                if (otherVn.getOffset() > calc_mask(sizeout)) return 0;
                if (2 * sizeout > multSize) return 0;
            }
            else if (otherVn.isWritten())
            {
                PcodeOp* zextOp = otherVn.getDef();
                if (zextOp.code() != CPUI_INT_ZEXT) return 0;
                if (zextOp.getIn(0).getSize() + sizeout > multSize) return 0;
            }
            else
                return 0;
            data.opSetInput(op, a, 0);
            return 0;
        }
    }
}
