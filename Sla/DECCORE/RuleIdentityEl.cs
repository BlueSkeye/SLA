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
    internal class RuleIdentityEl : Rule
    {
        public RuleIdentityEl(string g)
            : base(g, 0, "identityel")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleIdentityEl(getGroup());
        }

        /// \class RuleIdentityEl
        /// \brief Collapse operations using identity element:  `V + 0  =>  V`
        ///
        /// Similarly:
        ///   - `V ^ 0  =>  V`
        ///   - `V | 0  =>  V`
        ///   - `V || 0 =>  V`
        ///   - `V ^^ 0 =>  V`
        ///   - `V * 1  =>  V`
        public override void getOpList(List<uint4> oplist)
        {
            uint4 list[] = { CPUI_INT_ADD, CPUI_INT_XOR, CPUI_INT_OR,
          CPUI_BOOL_XOR, CPUI_BOOL_OR, CPUI_INT_MULT };
            oplist.insert(oplist.end(), list, list + 6);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constvn;
            uintb val;

            constvn = op->getIn(1);
            if (!constvn->isConstant()) return 0;
            val = constvn->getOffset();
            if ((val == 0) && (op->code() != CPUI_INT_MULT))
            {
                data.opSetOpcode(op, CPUI_COPY);
                data.opRemoveInput(op, 1); // Remove identity from operation
                return 1;
            }
            if (op->code() != CPUI_INT_MULT) return 0;
            if (val == 1)
            {
                data.opSetOpcode(op, CPUI_COPY);
                data.opRemoveInput(op, 1);
                return 1;
            }
            if (val == 0)
            {       // Multiply by zero
                data.opSetOpcode(op, CPUI_COPY);
                data.opRemoveInput(op, 0);
                return 1;
            }
            return 0;
        }
    }
}
