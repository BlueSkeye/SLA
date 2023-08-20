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
    internal class RuleNotDistribute : Rule
    {
        public RuleNotDistribute(string g)
            : base(g, 0, "notdistribute")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleNotDistribute(getGroup());
        }

        /// \class RuleNotDistribute
        /// \brief Distribute BOOL_NEGATE:  `!(V && W)  =>  !V || !W`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_BOOL_NEGATE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp? compop = op.getIn(0).getDef();
            PcodeOp newneg1, newneg2;
            Varnode newout1, newout2;
            OpCode opc;

            if (compop == (PcodeOp)null) return 0;
            switch (compop.code()) {
                case OpCode.CPUI_BOOL_AND:
                    opc = OpCode.CPUI_BOOL_OR;
                    break;
                case OpCode.CPUI_BOOL_OR:
                    opc = OpCode.CPUI_BOOL_AND;
                    break;
                default:
                    return 0;
            }

            newneg1 = data.newOp(1, op.getAddr());
            newout1 = data.newUniqueOut(1, newneg1);
            data.opSetOpcode(newneg1, OpCode.CPUI_BOOL_NEGATE);
            data.opSetInput(newneg1, compop.getIn(0), 0);
            data.opInsertBefore(newneg1, op);

            newneg2 = data.newOp(1, op.getAddr());
            newout2 = data.newUniqueOut(1, newneg2);
            data.opSetOpcode(newneg2, OpCode.CPUI_BOOL_NEGATE);
            data.opSetInput(newneg2, compop.getIn(1), 0);
            data.opInsertBefore(newneg2, op);

            data.opSetOpcode(op, opc);
            data.opSetInput(op, newout1, 0);
            data.opInsertInput(op, newout2, 1);
            return 1;
        }
    }
}
