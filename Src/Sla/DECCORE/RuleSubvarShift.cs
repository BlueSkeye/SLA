﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSubvarShift : Rule
    {
        public RuleSubvarShift(string g)
            : base(g, 0, "subvar_shift")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubvarShift(getGroup());
        }

        /// \class RuleSubvarShift
        /// \brief Perform SubvariableFlow analysis triggered by INT_RIGHT
        ///
        /// If the INT_RIGHT input has only 1 bit that can possibly be non-zero
        /// and it is getting shifted into the least significant bit position,
        /// trigger the full SubvariableFlow analysis.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn = op.getIn(0);
            if (vn.getSize() != 1)
                return 0;
            if (!op.getIn(1).isConstant())
                return 0;
            int sa = (int)op.getIn(1).getOffset();
            ulong mask = vn.getNZMask();
            if ((mask >> sa) != (ulong)1)
                // Pulling out a single bit
                return 0;
            mask = (mask >> sa) << sa;
            if (op.getOut().hasNoDescend())
                return 0;

            SubvariableFlow subflow = new SubvariableFlow(data,vn,mask,false,false,false);
            if (!subflow.doTrace())
                return 0;
            subflow.doReplacement();
            return 1;
        }
    }
}
