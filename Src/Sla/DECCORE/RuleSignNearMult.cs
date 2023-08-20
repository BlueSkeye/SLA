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
    internal class RuleSignNearMult : Rule
    {
        public RuleSignNearMult(string g)
            : base(g, 0, "signnearmult")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSignNearMult(getGroup());
        }

        /// \class RuleSignNearMult
        /// \brief Simplify division form: `(V + (V s>> 0x1f)>>(32-n)) & (-1<<n)  =>  (V s/ 2^n) * 2^n`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_AND);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant()) return 0;
            if (!op.getIn(0).isWritten()) return 0;
            PcodeOp addop = op.getIn(0).getDef();
            if (addop.code() != OpCode.CPUI_INT_ADD) return 0;
            Varnode shiftvn;
            PcodeOp unshiftop = (PcodeOp)null;
            int i;
            for (i = 0; i < 2; ++i)
            {
                shiftvn = addop.getIn(i);
                if (!shiftvn.isWritten()) continue;
                unshiftop = shiftvn.getDef();
                if (unshiftop.code() == OpCode.CPUI_INT_RIGHT)
                {
                    if (!unshiftop.getIn(1).isConstant()) continue;
                    break;
                }
            }
            if (i == 2) return 0;
            Varnode x = addop.getIn(1 - i);
            if (x.isFree()) return 0;
            int n = unshiftop.getIn(1).getOffset();
            if (n <= 0) return 0;
            n = shiftvn.getSize() * 8 - n;
            if (n <= 0) return 0;
            ulong mask = Globals.calc_mask(shiftvn.getSize());
            mask = (mask << n) & mask;
            if (mask != op.getIn(1).getOffset()) return 0;
            Varnode sgnvn = unshiftop.getIn(0);
            if (!sgnvn.isWritten()) return 0;
            PcodeOp sshiftop = sgnvn.getDef();
            if (sshiftop.code() != OpCode.CPUI_INT_SRIGHT) return 0;
            if (!sshiftop.getIn(1).isConstant()) return 0;
            if (sshiftop.getIn(0) != x) return 0;
            int val = sshiftop.getIn(1).getOffset();
            if (val != 8 * x.getSize() - 1) return 0;

            ulong pow = 1;
            pow <<= n;
            PcodeOp newdiv = data.newOp(2, op.getAddr());
            data.opSetOpcode(newdiv, OpCode.CPUI_INT_SDIV);
            Varnode divvn = data.newUniqueOut(x.getSize(), newdiv);
            data.opSetInput(newdiv, x, 0);
            data.opSetInput(newdiv, data.newConstant(x.getSize(), pow), 1);
            data.opInsertBefore(newdiv, op);

            data.opSetOpcode(op, OpCode.CPUI_INT_MULT);
            data.opSetInput(op, divvn, 0);
            data.opSetInput(op, data.newConstant(x.getSize(), pow), 1);
            return 1;
        }
    }
}
