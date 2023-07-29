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
    internal class RuleLeftRight : Rule
    {
        public RuleLeftRight(string g)
            : base(g, 0, "leftright")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleLeftRight(getGroup());
        }

        /// \class RuleLeftRight
        /// \brief Transform canceling INT_RIGHT or INT_SRIGHT of INT_LEFT
        ///
        /// This works for both signed and unsigned right shifts. The shift
        /// amount must be a multiple of 8.
        ///
        /// `(V << c) s>> c  =>  sext( sub(V, #0) )`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_RIGHT);
            oplist.push_back(CPUI_INT_SRIGHT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            if (!op->getIn(1)->isConstant()) return 0;

            Varnode* shiftin = op->getIn(0);
            if (!shiftin->isWritten()) return 0;
            PcodeOp* leftshift = shiftin->getDef();
            if (leftshift->code() != CPUI_INT_LEFT) return 0;
            if (!leftshift->getIn(1)->isConstant()) return 0;
            uintb sa = op->getIn(1)->getOffset();
            if (leftshift->getIn(1)->getOffset() != sa) return 0; // Left shift must be by same amount

            if ((sa & 7) != 0) return 0;    // Must be multiple of 8
            int4 isa = (int4)(sa >> 3);
            int4 tsz = shiftin->getSize() - isa;
            if ((tsz != 1) && (tsz != 2) && (tsz != 4) && (tsz != 8)) return 0;

            if (shiftin->loneDescend() != op) return 0;
            Address addr = shiftin->getAddr();
            if (addr.isBigEndian())
                addr = addr + isa;
            data.opUnsetInput(op, 0);
            data.opUnsetOutput(leftshift);
            addr.renormalize(tsz);
            Varnode* newvn = data.newVarnodeOut(tsz, addr, leftshift);
            data.opSetOpcode(leftshift, CPUI_SUBPIECE);
            data.opSetInput(leftshift, data.newConstant(leftshift->getIn(1)->getSize(), 0), 1);
            data.opSetInput(op, newvn, 0);
            data.opRemoveInput(op, 1);  // Remove the right-shift constant
            data.opSetOpcode(op, (op->code() == CPUI_INT_SRIGHT) ? CPUI_INT_SEXT : CPUI_INT_ZEXT);
            return 1;
        }
    }
}
