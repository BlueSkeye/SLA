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
    internal class RuleTrivialShift : Rule
    {
        public RuleTrivialShift(string g)
            : base(g, 0, "trivialshift")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleTrivialShift(getGroup());
        }

        /// \class RuleTrivialShift
        /// \brief Simplify trivial shifts:  `V << 0  =>  V,  V << #64  =>  0`
        public override void getOpList(List<uint4> oplist)
        {
            uint4 list[] = { CPUI_INT_LEFT, CPUI_INT_RIGHT, CPUI_INT_SRIGHT };
            oplist.insert(oplist.end(), list, list + 3);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            uintb val;
            Varnode* constvn = op.getIn(1);
            if (!constvn.isConstant()) return 0;   // Must shift by a constant
            val = constvn.getOffset();
            if (val != 0)
            {
                Varnode* replace;
                if (val < 8 * op.getIn(0).getSize()) return 0;    // Non-trivial
                if (op.code() == CPUI_INT_SRIGHT) return 0; // Cant predict signbit
                replace = data.newConstant(op.getIn(0).getSize(), 0);
                data.opSetInput(op, replace, 0);
            }
            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, CPUI_COPY);
            return 1;
        }
    }
}
