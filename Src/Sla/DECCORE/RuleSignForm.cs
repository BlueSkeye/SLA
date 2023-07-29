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
    internal class RuleSignForm : Rule
    {
        public RuleSignForm(string g)
            : base(g, 0, "signform")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSignForm(getGroup());
        }

        /// \class RuleSignForm
        /// \brief Normalize sign extraction:  `sub(sext(V),c)  =>  V s>> 31`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* sextout,*a;
            PcodeOp* sextop;

            sextout = op.getIn(0);
            if (!sextout.isWritten()) return 0;
            sextop = sextout.getDef();
            if (sextop.code() != CPUI_INT_SEXT)
                return 0;
            a = sextop.getIn(0);
            int c = op.getIn(1).getOffset();
            if (c < a.getSize()) return 0;
            if (a.isFree()) return 0;

            data.opSetInput(op, a, 0);
            int n = 8 * a.getSize() - 1;
            data.opSetInput(op, data.newConstant(4, n), 1);
            data.opSetOpcode(op, CPUI_INT_SRIGHT);
            return 1;
        }
    }
}
