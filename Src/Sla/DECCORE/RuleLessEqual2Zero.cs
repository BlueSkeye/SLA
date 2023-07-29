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
    internal class RuleLessEqual2Zero : Rule
    {
        public RuleLessEqual2Zero(string g)
            : base(g, 0, "lessequal2zero")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleLessEqual2Zero(getGroup());
        }

        /// \class RuleLessEqual2Zero
        /// \brief Simplify INT_LESSEQUAL applied to extremal constants
        ///
        /// Forms include:
        ///  - `0 <= V  =>  true`
        ///  - `V <= 0  =>  V == 0`
        ///  - `ffff <= V  =>  ffff == V`
        ///  - `V <= ffff` =>  true`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_LESSEQUAL);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* lvn,*rvn;
            lvn = op.getIn(0);
            rvn = op.getIn(1);

            if (lvn.isConstant())
            {
                if (lvn.getOffset() == 0)
                {
                    data.opSetOpcode(op, CPUI_COPY); // All values => true
                    data.opRemoveInput(op, 1);
                    data.opSetInput(op, data.newConstant(1, 1), 0);
                    return 1;
                }
                else if (lvn.getOffset() == calc_mask(lvn.getSize()))
                {
                    data.opSetOpcode(op, CPUI_INT_EQUAL); // No value is true except -1
                    return 1;
                }
            }
            else if (rvn.isConstant())
            {
                if (rvn.getOffset() == 0)
                {
                    data.opSetOpcode(op, CPUI_INT_EQUAL); // No value is true except 0
                    return 1;
                }
                else if (rvn.getOffset() == calc_mask(rvn.getSize()))
                {
                    data.opSetOpcode(op, CPUI_COPY); // All values => true
                    data.opRemoveInput(op, 1);
                    data.opSetInput(op, data.newConstant(1, 1), 0);
                    return 1;
                }
            }
            return 0;
        }
    }
}
