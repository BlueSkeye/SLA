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
    internal class RuleAndOrLump : Rule
    {
        public RuleAndOrLump(string g)
            : base(g, 0, "andorlump")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleAndOrLump(getGroup());
        }

        /// \class RuleAndOrLump
        /// \brief Collapse constants in logical expressions:  `(V & c) & d  =>  V & (c & d)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_AND);
            oplist.Add(OpCode.CPUI_INT_OR);
            oplist.Add(OpCode.CPUI_INT_XOR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            OpCode opc = op.code();
            if (!op.getIn(1).isConstant()) return 0;
            Varnode vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            PcodeOp op2 = vn1.getDef() ?? throw new BugException();
            if (op2.code() != opc) return 0; // Must be same op
            if (!op2.getIn(1).isConstant()) return 0;
            Varnode basevn = op2.getIn(0);
            if (basevn.isFree()) return 0;

            ulong val = op.getIn(1).getOffset();
            ulong val2 = op2.getIn(1).getOffset();
            if (opc == OpCode.CPUI_INT_AND)
                val &= val2;
            else if (opc == OpCode.CPUI_INT_OR)
                val |= val2;
            else if (opc == OpCode.CPUI_INT_XOR)
                val ^= val2;

            data.opSetInput(op, basevn, 0);
            data.opSetInput(op, data.newConstant(basevn.getSize(), val), 1);
            return 1;
        }
    }
}
