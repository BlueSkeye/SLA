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
    internal class RuleLogic2Bool : Rule
    {
        public RuleLogic2Bool(string g)
            : base(g, 0, "logic2bool")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleLogic2Bool(getGroup());
        }

        /// \class RuleLogic2Bool
        /// \brief Convert logical to boolean operations:  `V & W  =>  V && W,  V | W  => V || W`
        ///
        /// Verify that the inputs to the logical operator are booleans, then convert
        /// INT_AND to BOOL_AND, INT_OR to BOOL_OR etc.
        public override void getOpList(List<OpCode> oplist)
        {
            OpCode[] list = { OpCode.CPUI_INT_AND, OpCode.CPUI_INT_OR, OpCode.CPUI_INT_XOR };
            oplist.AddRange(list);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode boolVn;

            boolVn = op.getIn(0);
            if (!boolVn.isBooleanValue(data.isTypeRecoveryOn())) return 0;
            Varnode in1 = op.getIn(1);
            if (in1.isConstant()) {
                if (in1.getOffset() > (ulong)1) // If one side is a constant 0 or 1, this is boolean
                    return 0;
            }
            else if (!in1.isBooleanValue(data.isTypeRecoveryOn())) {
                return 0;
            }
            switch (op.code()) {
                case OpCode.CPUI_INT_AND:
                    data.opSetOpcode(op, OpCode.CPUI_BOOL_AND);
                    break;
                case OpCode.CPUI_INT_OR:
                    data.opSetOpcode(op, OpCode.CPUI_BOOL_OR);
                    break;
                case OpCode.CPUI_INT_XOR:
                    data.opSetOpcode(op, OpCode.CPUI_BOOL_XOR);
                    break;
                default:
                    return 0;
            }
            return 1;
        }
    }
}
