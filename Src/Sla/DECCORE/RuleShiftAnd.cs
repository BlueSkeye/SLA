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

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleShiftAnd(getGroup());
        }

        /// \class RuleShiftAnd
        /// \brief Eliminate any INT_AND when the bits it zeroes out are discarded by a shift
        ///
        /// This also allows for bits that aren't discarded but are already zero.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_RIGHT);
            oplist.push_back(CPUI_INT_LEFT);
            oplist.push_back(CPUI_INT_MULT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* cvn = op.getIn(1);
            if (!cvn.isConstant()) return 0;
            Varnode* shiftin = op.getIn(0);
            if (!shiftin.isWritten()) return 0;
            PcodeOp* andop = shiftin.getDef();
            if (andop.code() != CPUI_INT_AND) return 0;
            if (shiftin.loneDescend() != op) return 0;
            Varnode* maskvn = andop.getIn(1);
            if (!maskvn.isConstant()) return 0;
            uintb mask = maskvn.getOffset();
            Varnode* invn = andop.getIn(0);
            if (invn.isFree()) return 0;

            OpCode opc = op.code();
            int4 sa;
            if ((opc == CPUI_INT_RIGHT) || (opc == CPUI_INT_LEFT))
                sa = (int4)cvn.getOffset();
            else
            {
                sa = leastsigbit_set(cvn.getOffset()); // Make sure the multiply is really a shift
                if (sa <= 0) return 0;
                uintb testval = 1;
                testval <<= sa;
                if (testval != cvn.getOffset()) return 0;
                opc = CPUI_INT_LEFT;    // Treat CPUI_INT_MULT as CPUI_INT_LEFT
            }
            uintb nzm = invn.getNZMask();
            uintb fullmask = calc_mask(invn.getSize());
            if (opc == CPUI_INT_RIGHT)
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
            data.opSetOpcode(andop, CPUI_COPY); // AND effectively does nothing, so we change it to a copy
            data.opRemoveInput(andop, 1);
            return 1;
        }
    }
}
