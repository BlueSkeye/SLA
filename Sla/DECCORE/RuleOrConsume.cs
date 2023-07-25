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
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleOrConsume(getGroup());
        }

        /// \class RuleOrConsume
        /// \brief Simply OR with unconsumed input:  `V = A | B  =>  V = B  if  nzm(A) & consume(V) == 0
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_OR);
            oplist.push_back(CPUI_INT_XOR);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* outvn = op->getOut();
            int4 size = outvn->getSize();
            if (size > sizeof(uintb)) return 0; // FIXME: uintb should be arbitrary precision
            uintb consume = outvn->getConsume();
            if ((consume & op->getIn(0)->getNZMask()) == 0)
            {
                data.opRemoveInput(op, 0);
                data.opSetOpcode(op, CPUI_COPY);
                return 1;
            }
            else if ((consume & op->getIn(1)->getNZMask()) == 0)
            {
                data.opRemoveInput(op, 1);
                data.opSetOpcode(op, CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
