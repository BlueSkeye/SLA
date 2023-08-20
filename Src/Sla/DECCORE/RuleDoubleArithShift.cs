using ghidra;
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
    internal class RuleDoubleArithShift : Rule
    {
        public RuleDoubleArithShift(string g)
            : base(g, 0, "doublearithshift")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleDoubleArithShift(getGroup());
        }

        /// \class RuleDoubleArithShift
        /// \brief Simplify two sequential INT_SRIGHT: `(x s>> c) s>> d   =>  x s>> saturate(c + d)`
        ///
        /// Division optimization in particular can produce a sequence of signed right shifts.
        /// The shift amounts add up to the point where the sign bit has saturated the entire result.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode constD = op.getIn(1);
            if (!constD.isConstant()) return 0;
            Varnode shiftin = op.getIn(0);
            if (!shiftin.isWritten()) return 0;
            PcodeOp shift2op = shiftin.getDef();
            if (shift2op.code() != OpCode.CPUI_INT_SRIGHT) return 0;
            Varnode constC = shift2op.getIn(1);
            if (!constC.isConstant()) return 0;
            Varnode inVn = shift2op.getIn(0);
            if (inVn.isFree()) return 0;
            int max = op.getOut().getSize() * 8 - 1; // This is maximum possible shift.
            int sa = (int)constC.getOffset() + (int)constD.getOffset();
            if (sa <= 0) return 0;  // Something is wrong
            if (sa > max)
                sa = max;           // Shift amount has saturated
            data.opSetInput(op, inVn, 0);
            data.opSetInput(op, data.newConstant(4, sa), 1);
            return 1;
        }
    }
}
