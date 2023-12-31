﻿using Sla.CORE;
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

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
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
        public override void getOpList(List<OpCode> oplist)
        {
            OpCode[] list = { OpCode.CPUI_INT_ADD, OpCode.CPUI_INT_XOR, OpCode.CPUI_INT_OR,
                OpCode.CPUI_BOOL_XOR, OpCode.CPUI_BOOL_OR, OpCode.CPUI_INT_MULT };
            oplist.AddRange(list);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            ulong val;

            Varnode constvn = op.getIn(1);
            if (!constvn.isConstant()) return 0;
            val = constvn.getOffset();
            if ((val == 0) && (op.code() != OpCode.CPUI_INT_MULT)) {
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opRemoveInput(op, 1); // Remove identity from operation
                return 1;
            }
            if (op.code() != OpCode.CPUI_INT_MULT) return 0;
            if (val == 1) {
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opRemoveInput(op, 1);
                return 1;
            }
            if (val == 0) {
                // Multiply by zero
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opRemoveInput(op, 0);
                return 1;
            }
            return 0;
        }
    }
}
