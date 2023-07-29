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
    internal class RuleCarryElim : Rule
    {
        public RuleCarryElim(string g)
            : base(g, 0, "carryelim")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleCarryElim(getGroup());
        }

        /// \class RuleCarryElim
        /// \brief Transform INT_CARRY using a constant:  `carry(V,c)  =>  -c <= V`
        ///
        /// There is a special case when the constant is zero:
        ///   - `carry(V,0)  => false`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_CARRY);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn1,*vn2;

            vn2 = op.getIn(1);
            if (!vn2.isConstant()) return 0;
            vn1 = op.getIn(0);
            if (vn1.isFree()) return 0;
            uintb off = vn2.getOffset();
            if (off == 0)
            {       // Trivial case
                data.opRemoveInput(op, 1);  // Go down to 1 input
                data.opSetInput(op, data.newConstant(1, 0), 0); // Put a boolean "false" as input to COPY
                data.opSetOpcode(op, CPUI_COPY);
                return 1;
            }
            off = (-off) & calc_mask(vn2.getSize()); // Take twos-complement of constant

            data.opSetOpcode(op, CPUI_INT_LESSEQUAL);
            data.opSetInput(op, vn1, 1);    // Move other input to second position
            data.opSetInput(op, data.newConstant(vn1.getSize(), off), 0); // Put the new constant in first position
            return 1;
        }
    }
}
