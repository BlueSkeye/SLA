﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleDoubleSub : Rule
    {
        public RuleDoubleSub(string g)
            : base(g, 0, "doublesub")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleDoubleSub(getGroup());
        }

        /// \class RuleDoubleSub
        /// \brief Simplify chained SUBPIECE:  `sub( sub(V,c), d)  =>  sub(V, c+d)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp op2;
            Varnode vn;
            int offset1, offset2;

            vn = op.getIn(0) ?? throw new ApplicationException();
            if (!vn.isWritten()) return 0;
            op2 = vn.getDef() ?? throw new ApplicationException();
            if (op2.code() != OpCode.CPUI_SUBPIECE) return 0;
            offset1 = (int)(op.getIn(1).getOffset());
            offset2 = (int)(op2.getIn(1).getOffset());

            data.opSetInput(op, op2.getIn(0), 0);  // Skip middleman
            data.opSetInput(op, data.newConstant(4, (ulong)(offset1 + offset2)), 1);
            return 1;
        }
    }
}
