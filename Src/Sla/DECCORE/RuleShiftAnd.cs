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
    internal class RuleShiftAnd : Rule
    {
        public RuleShiftAnd(string g)
            : base(g, 0, "shiftand")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleShiftAnd(getGroup());
        }

        /// \class RuleShiftAnd
        /// \brief Eliminate any INT_AND when the bits it zeroes out are discarded by a shift
        ///
        /// This also allows for bits that aren't discarded but are already zero.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
            oplist.Add(OpCode.CPUI_INT_LEFT);
            oplist.Add(OpCode.CPUI_INT_MULT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode cvn = op.getIn(1);
            if (!cvn.isConstant()) return 0;
            Varnode shiftin = op.getIn(0);
            if (!shiftin.isWritten()) return 0;
            PcodeOp andop = shiftin.getDef();
            if (andop.code() != OpCode.CPUI_INT_AND) return 0;
            if (shiftin.loneDescend() != op) return 0;
            Varnode maskvn = andop.getIn(1);
            if (!maskvn.isConstant()) return 0;
            ulong mask = maskvn.getOffset();
            Varnode invn = andop.getIn(0);
            if (invn.isFree()) return 0;

            OpCode opc = op.code();
            int sa;
            if ((opc == OpCode.CPUI_INT_RIGHT) || (opc == OpCode.CPUI_INT_LEFT))
                sa = (int)cvn.getOffset();
            else
            {
                sa = Globals.leastsigbit_set(cvn.getOffset()); // Make sure the multiply is really a shift
                if (sa <= 0) return 0;
                ulong testval = 1;
                testval <<= sa;
                if (testval != cvn.getOffset()) return 0;
                opc = OpCode.CPUI_INT_LEFT;    // Treat OpCode.CPUI_INT_MULT as OpCode.CPUI_INT_LEFT
            }
            ulong nzm = invn.getNZMask();
            ulong fullmask = Globals.calc_mask(invn.getSize());
            if (opc == OpCode.CPUI_INT_RIGHT)
            {
                nzm >>= sa;
                mask >>= sa;
            }
            else
            {
                nzm <<= sa;
                mask <<= sa;
                nzm &= fullmask;
                mask &= fullmask;
            }
            if ((mask & nzm) != nzm) return 0;
            data.opSetOpcode(andop, OpCode.CPUI_COPY); // AND effectively does nothing, so we change it to a copy
            data.opRemoveInput(andop, 1);
            return 1;
        }
    }
}
