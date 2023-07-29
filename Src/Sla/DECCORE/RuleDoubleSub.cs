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
    internal class RuleDoubleSub : Rule
    {
        public RuleDoubleSub(string g)
            : base(g, 0, "doublesub")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleDoubleSub(getGroup());
        }

        /// \class RuleDoubleSub
        /// \brief Simplify chained SUBPIECE:  `sub( sub(V,c), d)  =>  sub(V, c+d)`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* op2;
            Varnode* vn;
            int offset1, offset2;

            vn = op.getIn(0);
            if (!vn.isWritten()) return 0;
            op2 = vn.getDef();
            if (op2.code() != CPUI_SUBPIECE) return 0;
            offset1 = op.getIn(1).getOffset();
            offset2 = op2.getIn(1).getOffset();

            data.opSetInput(op, op2.getIn(0), 0);  // Skip middleman
            data.opSetInput(op, data.newConstant(4, offset1 + offset2), 1);
            return 1;
        }
    }
}
