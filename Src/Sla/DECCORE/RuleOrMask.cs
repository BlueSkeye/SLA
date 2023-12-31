﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleOrMask : Rule
    {
        public RuleOrMask(string g)
            : base(g, 0, "ormask")
        {
        }


        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleOrMask(getGroup());
        }

        /// \class RuleOrMask
        /// \brief Simplify INT_OR with full mask:  `V = W | 0xffff  =>  V = W`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_OR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int size = op.getOut().getSize();
            if (size > sizeof(ulong)) return 0; // FIXME: ulong should be arbitrary precision
            Varnode constvn;

            constvn = op.getIn(1) ?? throw new ApplicationException();
            if (!constvn.isConstant()) return 0;
            ulong val = constvn.getOffset();
            ulong mask = Globals.calc_mask((uint)size);
            if ((val & mask) != mask) return 0;
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            data.opSetInput(op, constvn, 0);
            data.opRemoveInput(op, 1);
            return 1;
        }
    }
}
