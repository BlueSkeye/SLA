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
    internal class RuleConcatZext : Rule
    {
        public RuleConcatZext(string g)
            : base(g, 0, "concatzext")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleConcatZext(getGroup());
        }

        /// \class RuleConcatZext
        /// \brief Commute PIECE with INT_ZEXT:  `concat(zext(V),W)  =>  zext(concat(V,W))`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_PIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode hi, lo;

            hi = op.getIn(0);
            if (!hi.isWritten()) return 0;
            PcodeOp zextop = hi.getDef() ?? throw new BugException();
            if (zextop.code() != OpCode.CPUI_INT_ZEXT) return 0;
            hi = zextop.getIn(0);
            lo = op.getIn(1);
            if (hi.isFree()) return 0;
            if (lo.isFree()) return 0;

            // Create new (earlier) concat out of hi and lo
            PcodeOp newconcat = data.newOp(2, op.getAddr());
            data.opSetOpcode(newconcat, OpCode.CPUI_PIECE);
            Varnode newvn = data.newUniqueOut(hi.getSize() + lo.getSize(), newconcat);
            data.opSetInput(newconcat, hi, 0);
            data.opSetInput(newconcat, lo, 1);
            data.opInsertBefore(newconcat, op);

            // Change original op into a ZEXT
            data.opRemoveInput(op, 1);
            data.opSetInput(op, newvn, 0);
            data.opSetOpcode(op, OpCode.CPUI_INT_ZEXT);
            return 1;
        }
    }
}
