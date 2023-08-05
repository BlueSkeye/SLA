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
    internal class RuleShiftSub : Rule
    {
        public RuleShiftSub(string g)
            : base(g, 0, "shiftsub")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleShiftSub(getGroup());
        }

        /// \class RuleShiftSub
        /// \brief Simplify SUBPIECE applied to INT_LEFT: `sub( V << 8*k, c)  =>  sub(V,c-k)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(0).isWritten()) return 0;
            PcodeOp* shiftop = op.getIn(0).getDef();
            if (shiftop.code() != OpCode.CPUI_INT_LEFT) return 0;
            Varnode* sa = shiftop.getIn(1);
            if (!sa.isConstant()) return 0;
            int n = sa.getOffset();
            if ((n & 7) != 0) return 0;     // Must shift by a multiple of 8 bits
            int c = op.getIn(1).getOffset();
            Varnode* vn = shiftop.getIn(0);
            if (vn.isFree()) return 0;
            int insize = vn.getSize();
            int outsize = op.getOut().getSize();
            c -= n / 8;
            if (c < 0 || c + outsize > insize)  // Check if this is a natural truncation
                return 0;
            data.opSetInput(op, vn, 0);
            data.opSetInput(op, data.newConstant(op.getIn(1).getSize(), c), 1);
            return 1;
        }
    }
}
