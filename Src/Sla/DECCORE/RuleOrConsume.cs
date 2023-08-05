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
    internal class RuleOrConsume : Rule
    {
        public RuleOrConsume(string g)
            : base(g, 0, "orconsume")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleOrConsume(getGroup());
        }

        /// \class RuleOrConsume
        /// \brief Simply OR with unconsumed input:  `V = A | B  =>  V = B  if  nzm(A) & consume(V) == 0
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_OR);
            oplist.Add(CPUI_INT_XOR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* outvn = op.getOut();
            int size = outvn.getSize();
            if (size > sizeof(ulong)) return 0; // FIXME: ulong should be arbitrary precision
            ulong consume = outvn.getConsume();
            if ((consume & op.getIn(0).getNZMask()) == 0)
            {
                data.opRemoveInput(op, 0);
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                return 1;
            }
            else if ((consume & op.getIn(1).getNZMask()) == 0)
            {
                data.opRemoveInput(op, 1);
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
