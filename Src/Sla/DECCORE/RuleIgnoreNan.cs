﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleIgnoreNan : Rule
    {
        public RuleIgnoreNan(string g)
            : base(g, 0, "ignorenan")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleIgnoreNan(getGroup());
        }

        /// \class RuleIgnoreNan
        /// \brief Treat FLOAT_NAN as always evaluating to false
        ///
        /// This makes the assumption that all floating-point calculations
        /// give valid results (not NaN).
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_FLOAT_NAN);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (op.numInput() == 2)
                data.opRemoveInput(op, 1);

            // Treat these operations as always returning false (0)
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            data.opSetInput(op, data.newConstant(1, 0), 0);
            return 1;
        }
    }
}
