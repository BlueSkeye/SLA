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
    internal class RuleZextSless : Rule
    {
        public RuleZextSless(string g)
            : base(g, 0, "zextsless")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleZextSless(getGroup());
        }

        /// \class RuleZextSless
        /// \brief Transform INT_ZEXT and INT_SLESS:  `zext(V) s< c  =>  V < c`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SLESS);
            oplist.Add(OpCode.CPUI_INT_SLESSEQUAL);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp zext;
            Varnode vn1, vn2;
            int smallsize, zextslot, otherslot;
            ulong val;

            vn1 = op.getIn(0);
            vn2 = op.getIn(1);
            zextslot = 0;
            otherslot = 1;
            if ((vn2.isWritten()) && (vn2.getDef().code() == OpCode.CPUI_INT_ZEXT)) {
                vn1 = vn2;
                vn2 = op.getIn(0);
                zextslot = 1;
                otherslot = 0;
            }
            else if ((!vn1.isWritten()) || (vn1.getDef().code() != OpCode.CPUI_INT_ZEXT))
                return false;

            if (!vn2.isConstant()) return false;
            zext = vn1.getDef();
            if (!zext.getIn(0).isHeritageKnown()) return false;

            smallsize = zext.getIn(0).getSize();
            val = vn2.getOffset();
            if ((val >> (8 * smallsize - 1)) != 0) return false; // Is zero extension unnecessary, sign bit must also be 0

            Varnode newvn = data.newConstant(smallsize, val);
            data.opSetInput(op, zext.getIn(0), zextslot);
            data.opSetInput(op, newvn, otherslot); ;
            data.opSetOpcode(op, (op.code() == OpCode.CPUI_INT_SLESS) ? OpCode.CPUI_INT_LESS : OpCode.CPUI_INT_LESSEQUAL);
            return true;
        }
    }
}
