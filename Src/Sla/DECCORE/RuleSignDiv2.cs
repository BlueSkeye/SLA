using Sla.CORE;
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

        public override Rule? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? (Rule)null : new RuleSignDiv2(getGroup());
        }

        /// \class RuleSignDiv2
        /// \brief Convert INT_SRIGHT form into INT_SDIV:  `(V + -1*(V s>> 31)) s>> 1  =>  V s/ 2`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode addout;
            Varnode multout;
            Varnode shiftout;

            if (!op.getIn(1).isConstant()) return 0;
            if (op.getIn(1).getOffset() != 1) return 0;
            addout = op.getIn(0);
            if (!addout.isWritten()) return 0;
            PcodeOp addop = addout.getDef() ?? throw new BugException();
            if (addop.code() != OpCode.CPUI_INT_ADD) return 0;
            int i;
            Varnode? a = (Varnode)null;
            for (i = 0; i < 2; ++i) {
                multout = addop.getIn(i);
                if (!multout.isWritten()) continue;
                PcodeOp multop = multout.getDef() ?? throw new BugException();
                if (multop.code() != OpCode.CPUI_INT_MULT)
                    continue;
                if (!multop.getIn(1).isConstant()) continue;
                if (multop.getIn(1).getOffset() !=
                Globals.calc_mask((uint)multop.getIn(1).getSize()))
                    continue;
                shiftout = multop.getIn(0);
                if (!shiftout.isWritten()) continue;
                PcodeOp shiftop = shiftout.getDef() ?? throw new BugException();
                if (shiftop.code() != OpCode.CPUI_INT_SRIGHT)
                    continue;
                if (!shiftop.getIn(1).isConstant()) continue;
                int n = (int)shiftop.getIn(1).getOffset();
                a = shiftop.getIn(0);
                if (a != addop.getIn(1 - i)) continue;
                if (n != 8 * a.getSize() - 1) continue;
                if (a.isFree()) continue;
                break;
            }
            if (i == 2) return 0;

            data.opSetInput(op, a, 0);
            data.opSetInput(op, data.newConstant(a.getSize(), 2), 1);
            data.opSetOpcode(op, OpCode.CPUI_INT_SDIV);
            return 1;
        }
    }
}
