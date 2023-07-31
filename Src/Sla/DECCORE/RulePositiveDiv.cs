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
    internal class RulePositiveDiv : Rule
    {
        public RulePositiveDiv(string g)
            : base(g, 0, "positivediv")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePositiveDiv(getGroup());
        }

        /// \class RulePositiveDiv
        /// \brief Signed division of positive values is unsigned division
        ///
        /// If the sign bit of both the numerator and denominator of a signed division (or remainder)
        /// are zero, then convert to the unsigned form of the operation.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_SDIV);
            oplist.Add(CPUI_INT_SREM);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int sa = op.getOut().getSize();
            if (sa > sizeof(ulong)) return 0;
            sa = sa * 8 - 1;
            if (((op.getIn(0).getNZMask() >> sa) & 1) != 0)
                return 0;       // Input 0 may be negative
            if (((op.getIn(1).getNZMask() >> sa) & 1) != 0)
                return 0;       // Input 1 may be negative
            OpCode opc = (op.code() == OpCode.CPUI_INT_SDIV) ? OpCode.CPUI_INT_DIV : OpCode.CPUI_INT_REM;
            data.opSetOpcode(op, opc);
            return 1;
        }
    }
}
