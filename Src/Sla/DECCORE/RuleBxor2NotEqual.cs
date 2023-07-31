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
    internal class RuleBxor2NotEqual : Rule
    {
        public RuleBxor2NotEqual(string g)
            : base(g, 0, "bxor2notequal")
        {
        }

        public override Rule clone(ActionGroupList grouplist) {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleBxor2NotEqual(getGroup());
        }

        /// \class RuleBxor2NotEqual
        /// \brief Eliminate BOOL_XOR:  `V ^^ W  =>  V != W`
        public override getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_BOOL_XOR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            data.opSetOpcode(op, OpCode.CPUI_INT_NOTEQUAL);
            return 1;
        }
    }
}
