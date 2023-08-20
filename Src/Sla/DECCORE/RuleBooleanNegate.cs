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
    internal class RuleBooleanNegate : Rule
    {
        public RuleBooleanNegate(string g)
            : base(g, 0, "booleannegate")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleBooleanNegate(getGroup());
        }

        /// \class RuleBooleanNegate
        /// \brief Simplify comparisons with boolean values:  `V == false  =>  !V,  V == true  =>  V`
        ///
        /// Works with both INT_EQUAL and INT_NOTEQUAL.  Both sides of the comparison
        /// must be boolean values.
        public override void getOpList(List<OpCode> oplist)
        {
            OpCode[] list = { OpCode.CPUI_INT_NOTEQUAL, OpCode.CPUI_INT_EQUAL };
            oplist.AddRange(list);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            OpCode opc;
            Varnode constvn;
            Varnode subbool;
            bool negate;
            ulong val;

            opc = op.code();
            constvn = op.getIn(1);
            subbool = op.getIn(0);
            if (!constvn.isConstant()) return 0;
            val = constvn.getOffset();
            if ((val != 0) && (val != 1))
                return 0;
            negate = (opc == OpCode.CPUI_INT_NOTEQUAL);
            if (val == 0)
                negate = !negate;

            if (!subbool.isBooleanValue(data.isTypeRecoveryOn())) return 0;

            data.opRemoveInput(op, 1);  // Remove second parameter
            data.opSetInput(op, subbool, 0); // Keep original boolean parameter
            if (negate)
                data.opSetOpcode(op, OpCode.CPUI_BOOL_NEGATE);
            else
                data.opSetOpcode(op, OpCode.CPUI_COPY);

            return 1;
        }
    }
}
