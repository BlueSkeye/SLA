using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RulePiece2Sext : Rule
    {
        public RulePiece2Sext(string g)
            : base(g, 0, "piece2sext")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePiece2Sext(getGroup());
        }

        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_PIECE);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode shiftout, x;

            shiftout = op.getIn(0);
            if (!shiftout.isWritten()) return 0;
            PcodeOp shiftop = shiftout.getDef() ?? throw new BugException();
            if (shiftop.code() != OpCode.CPUI_INT_SRIGHT) return 0;
            if (!shiftop.getIn(1).isConstant()) return 0;
            int n = shiftop.getIn(1).getOffset();
            x = shiftop.getIn(0);
            if (x != op.getIn(1)) return 0;
            if (n != 8 * x.getSize() - 1) return 0;

            data.opRemoveInput(op, 0);
            data.opSetOpcode(op, OpCode.CPUI_INT_SEXT);
            return 1;
        }
    }
}
