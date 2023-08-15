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
    internal class RuleNegateNegate : Rule
    {
        public RuleNegateNegate(string g)
            : base(g, 0, "negatenegate")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleNegateNegate(getGroup());
        }

        /// \class RuleNegateNegate
        /// \brief Simplify INT_NEGATE chains:  `~~V  =>  V`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_NEGATE);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            PcodeOp* neg2 = vn1.getDef();
            if (neg2.code() != OpCode.CPUI_INT_NEGATE)
                return 0;
            Varnode* vn2 = neg2.getIn(0);
            if (vn2.isFree()) return 0;
            data.opSetInput(op, vn2, 0);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            return 1;
        }
    }
}
