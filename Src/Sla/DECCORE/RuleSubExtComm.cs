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
    internal class RuleSubExtComm : Rule
    {
        public RuleSubExtComm(string g)
            : base(g,0,"subextcomm")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubExtComm(getGroup());
        }

        /// \class RuleSubExtComm
        /// \brief Commute SUBPIECE and INT_ZEXT:  `sub(zext(V),c)  =>  zext(sub(V,c))`
        ///
        /// This is in keeping with the philosophy to push SUBPIECE back earlier in the expression.
        /// The original SUBPIECE is changed into the INT_ZEXT, but the original INT_ZEXT is
        /// not changed, a new SUBPIECE is created.
        /// There are corner cases, if the SUBPIECE doesn't hit extended bits or is ultimately unnecessary.
        ///    - `sub(zext(V),c)  =>  sub(V,C)`
        ///    - `sub(zext(V),0)  =>  zext(V)`
        ///
        /// This rule also works with INT_SEXT.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode * base = op.getIn(0);
            if (!@base.isWritten()) return 0;
            PcodeOp extop = @base.getDef();
            if ((extop.code() != OpCode.CPUI_INT_ZEXT) && (extop.code() != OpCode.CPUI_INT_SEXT))
                return 0;
            Varnode invn = extop.getIn(0);
            if (invn.isFree()) return 0;
            int subcut = (int)op.getIn(1).getOffset();
            if (op.getOut().getSize() + subcut <= invn.getSize())
            {
                // SUBPIECE doesn't hit the extended bits at all
                data.opSetInput(op, invn, 0);
                if (invn.getSize() == op.getOut().getSize())
                {
                    data.opRemoveInput(op, 1);
                    data.opSetOpcode(op, OpCode.CPUI_COPY);
                }
                return 1;
            }

            if (subcut >= invn.getSize()) return 0;

            Varnode newvn;
            if (subcut != 0)
            {
                PcodeOp newop = data.newOp(2, op.getAddr());
                data.opSetOpcode(newop, OpCode.CPUI_SUBPIECE);
                newvn = data.newUniqueOut(invn.getSize() - subcut, newop);
                data.opSetInput(newop, data.newConstant(op.getIn(1).getSize(), (ulong)subcut), 1);
                data.opSetInput(newop, invn, 0);
                data.opInsertBefore(newop, op);
            }
            else
                newvn = invn;

            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, extop.code());
            data.opSetInput(op, newvn, 0);
            return 1;
        }
    }
}
