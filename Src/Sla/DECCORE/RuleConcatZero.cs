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
    internal class RuleConcatZero : Rule
    {
        public RuleConcatZero(string g)
            : base(g, 0, "concatzero")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleConcatZero(getGroup());
        }

        /// \class RuleConcatZero
        /// \brief Simplify concatenation with zero:  `concat(V,0)  =>  zext(V) << c`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_PIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant()) return 0;
            if (op.getIn(1).getOffset() != 0) return 0;

            int sa = 8 * op.getIn(1).getSize();
            Varnode highvn = op.getIn(0);
            PcodeOp newop = data.newOp(1, op.getAddr());
            Varnode outvn = data.newUniqueOut(op.getOut().getSize(), newop);
            data.opSetOpcode(newop, OpCode.CPUI_INT_ZEXT);
            data.opSetOpcode(op, OpCode.CPUI_INT_LEFT);
            data.opSetInput(op, outvn, 0);
            data.opSetInput(op, data.newConstant(4, sa), 1);
            data.opSetInput(newop, highvn, 0);
            data.opInsertBefore(newop, op);
            return 1;
        }
    }
}
