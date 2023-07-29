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
    internal class RuleLess2Zero : Rule
    {
        public RuleLess2Zero(string g)
            : base(g, 0, "less2zero")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleLess2Zero(getGroup());
        }

        /// \class RuleLess2Zero
        /// \brief Simplify INT_LESS applied to extremal constants
        ///
        /// Forms include:
        ///  - `0 < V  =>  0 != V`
        ///  - `V < 0  =>  false`
        ///  - `ffff < V  =>  false`
        ///  - `V < ffff` =>  V != ffff`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_LESS);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* lvn,*rvn;
            lvn = op.getIn(0);
            rvn = op.getIn(1);

            if (lvn.isConstant())
            {
                if (lvn.getOffset() == 0)
                {
                    data.opSetOpcode(op, CPUI_INT_NOTEQUAL); // All values except 0 are true   .  NOT_EQUAL
                    return 1;
                }
                else if (lvn.getOffset() == Globals.calc_mask(lvn.getSize()))
                {
                    data.opSetOpcode(op, CPUI_COPY); // Always false
                    data.opRemoveInput(op, 1);
                    data.opSetInput(op, data.newConstant(1, 0), 0);
                    return 1;
                }
            }
            else if (rvn.isConstant())
            {
                if (rvn.getOffset() == 0)
                {
                    data.opSetOpcode(op, CPUI_COPY); // Always false
                    data.opRemoveInput(op, 1);
                    data.opSetInput(op, data.newConstant(1, 0), 0);
                    return 1;
                }
                else if (rvn.getOffset() == Globals.calc_mask(rvn.getSize()))
                { // All values except -1 are true . NOT_EQUAL
                    data.opSetOpcode(op, CPUI_INT_NOTEQUAL);
                    return 1;
                }
            }
            return 0;
        }
    }
}
