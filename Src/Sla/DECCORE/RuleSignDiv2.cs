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
    internal class RuleSignDiv2 : Rule
    {
        public RuleSignDiv2(string g)
            : base(g, 0, "signdiv2")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSignDiv2(getGroup());
        }

        /// \class RuleSignDiv2
        /// \brief Convert INT_SRIGHT form into INT_SDIV:  `(V + -1*(V s>> 31)) s>> 1  =>  V s/ 2`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* addout,*multout,*shiftout,*a;
            PcodeOp* addop,*multop,*shiftop;

            if (!op.getIn(1).isConstant()) return 0;
            if (op.getIn(1).getOffset() != 1) return 0;
            addout = op.getIn(0);
            if (!addout.isWritten()) return 0;
            addop = addout.getDef();
            if (addop.code() != CPUI_INT_ADD) return 0;
            int i;
            a = (Varnode*)0;
            for (i = 0; i < 2; ++i)
            {
                multout = addop.getIn(i);
                if (!multout.isWritten()) continue;
                multop = multout.getDef();
                if (multop.code() != CPUI_INT_MULT)
                    continue;
                if (!multop.getIn(1).isConstant()) continue;
                if (multop.getIn(1).getOffset() !=
                calc_mask(multop.getIn(1).getSize()))
                    continue;
                shiftout = multop.getIn(0);
                if (!shiftout.isWritten()) continue;
                shiftop = shiftout.getDef();
                if (shiftop.code() != CPUI_INT_SRIGHT)
                    continue;
                if (!shiftop.getIn(1).isConstant()) continue;
                int n = shiftop.getIn(1).getOffset();
                a = shiftop.getIn(0);
                if (a != addop.getIn(1 - i)) continue;
                if (n != 8 * a.getSize() - 1) continue;
                if (a.isFree()) continue;
                break;
            }
            if (i == 2) return 0;

            data.opSetInput(op, a, 0);
            data.opSetInput(op, data.newConstant(a.getSize(), 2), 1);
            data.opSetOpcode(op, CPUI_INT_SDIV);
            return 1;
        }
    }
}
