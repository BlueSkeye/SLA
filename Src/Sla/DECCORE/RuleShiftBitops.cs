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
    internal class RuleShiftBitops : Rule
    {
        public RuleShiftBitops(string g)
            : base(g, 0, "shiftbitops")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleShiftBitops(getGroup());
        }

        /// \class RuleShiftBitops
        /// \brief Shifting away all non-zero bits of one-side of a logical/arithmetic op
        ///
        /// `( V & 0xf000 ) << 4   =>   #0 << 4`
        /// `( V + 0xf000 ) << 4   =>    V << 4`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_LEFT);
            oplist.push_back(CPUI_INT_RIGHT);
            oplist.push_back(CPUI_SUBPIECE);
            oplist.push_back(CPUI_INT_MULT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constvn = op.getIn(1);
            if (!constvn.isConstant()) return 0;   // Must be a constant shift
            Varnode* vn = op.getIn(0);
            if (!vn.isWritten()) return 0;
            if (vn.getSize() > sizeof(uintb)) return 0;    // FIXME: Can't exceed uintb precision
            int4 sa;
            bool leftshift;

            switch (op.code())
            {
                case CPUI_INT_LEFT:
                    sa = (int4)constvn.getOffset();
                    leftshift = true;
                    break;
                case CPUI_INT_RIGHT:
                    sa = (int4)constvn.getOffset();
                    leftshift = false;
                    break;
                case CPUI_SUBPIECE:
                    sa = (int4)constvn.getOffset();
                    sa = sa * 8;
                    leftshift = false;
                    break;
                case CPUI_INT_MULT:
                    sa = leastsigbit_set(constvn.getOffset());
                    if (sa == -1) return 0;
                    leftshift = true;
                    break;
                default:
                    return 0;           // Never reaches here
            }

            PcodeOp* bitop = vn.getDef();
            switch (bitop.code())
            {
                case CPUI_INT_AND:
                case CPUI_INT_OR:
                case CPUI_INT_XOR:
                    break;
                case CPUI_INT_MULT:
                case CPUI_INT_ADD:
                    if (!leftshift) return 0;
                    break;
                default:
                    return 0;
            }

            int4 i;
            for (i = 0; i < bitop.numInput(); ++i)
            {
                uintb nzm = bitop.getIn(i).getNZMask();
                uintb mask = calc_mask(op.getOut().getSize());
                if (leftshift)
                    nzm = pcode_left(nzm, sa);
                else
                    nzm = pcode_right(nzm, sa);
                if ((nzm & mask) == (uintb)0) break;
            }
            if (i == bitop.numInput()) return 0;
            switch (bitop.code())
            {
                case CPUI_INT_MULT:
                case CPUI_INT_AND:
                    vn = data.newConstant(vn.getSize(), 0);
                    data.opSetInput(op, vn, 0); // Result will be zero
                    break;
                case CPUI_INT_ADD:
                case CPUI_INT_XOR:
                case CPUI_INT_OR:
                    vn = bitop.getIn(1 - i);
                    if (!vn.isHeritageKnown()) return 0;
                    data.opSetInput(op, vn, 0);
                    break;
                default:
                    break;
            }
            return 1;
        }
    }
}
