﻿using Sla.CORE;
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

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSignForm(getGroup());
        }

        /// \class RuleSignForm
        /// \brief Normalize sign extraction:  `sub(sext(V),c)  =>  V s>> 31`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode sextout = op.getIn(0);
            if (!sextout.isWritten()) return 0;
            PcodeOp sextop = sextout.getDef() ?? throw new BugException();
            if (sextop.code() != OpCode.CPUI_INT_SEXT)
                return 0;
            Varnode a = sextop.getIn(0);
            int c = (int)op.getIn(1).getOffset();
            if (c < a.getSize()) return 0;
            if (a.isFree()) return 0;

            data.opSetInput(op, a, 0);
            int n = 8 * a.getSize() - 1;
            data.opSetInput(op, data.newConstant(4, (ulong)n), 1);
            data.opSetOpcode(op, OpCode.CPUI_INT_SRIGHT);
            return 1;
        }
    }
}
