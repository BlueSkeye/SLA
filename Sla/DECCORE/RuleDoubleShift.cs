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
    internal class RuleDoubleShift : Rule
    {
        public RuleDoubleShift(string g)
            : base(g, 0, "doubleshift")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleDoubleShift(getGroup());
        }

        /// \class RuleDoubleShift
        /// \brief Simplify chained shifts INT_LEFT and INT_RIGHT
        ///
        /// INT_MULT is considered a shift if it multiplies by a constant power of 2.
        /// The shifts can combine or cancel. Combined shifts may zero out result.
        ///
        ///    - `(V << c) << d  =>  V << (c+d)`
        ///    - `(V << c) >> c` =>  V & 0xff`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_LEFT);
            oplist.push_back(CPUI_INT_RIGHT);
            oplist.push_back(CPUI_INT_MULT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* secvn,*newvn;
            PcodeOp* secop;
            OpCode opc1, opc2;
            int4 sa1, sa2, size;
            uintb mask;

            if (!op->getIn(1)->isConstant()) return 0;
            secvn = op->getIn(0);
            if (!secvn->isWritten()) return 0;
            secop = secvn->getDef();
            opc2 = secop->code();
            if ((opc2 != CPUI_INT_LEFT) && (opc2 != CPUI_INT_RIGHT) && (opc2 != CPUI_INT_MULT))
                return 0;
            if (!secop->getIn(1)->isConstant()) return 0;
            opc1 = op->code();
            size = secvn->getSize();
            if (!secop->getIn(0)->isHeritageKnown()) return 0;

            if (opc1 == CPUI_INT_MULT)
            {
                uintb val = op->getIn(1)->getOffset();
                sa1 = leastsigbit_set(val);
                if ((val >> sa1) != (uintb)1) return 0; // Not multiplying by a power of 2
                opc1 = CPUI_INT_LEFT;
            }
            else
                sa1 = op->getIn(1)->getOffset();
            if (opc2 == CPUI_INT_MULT)
            {
                uintb val = secop->getIn(1)->getOffset();
                sa2 = leastsigbit_set(val);
                if ((val >> sa2) != (uintb)1) return 0; // Not multiplying by a power of 2
                opc2 = CPUI_INT_LEFT;
            }
            else
                sa2 = secop->getIn(1)->getOffset();
            if (opc1 == opc2)
            {
                if (sa1 + sa2 < 8 * size)
                {
                    newvn = data.newConstant(4, sa1 + sa2);
                    data.opSetOpcode(op, opc1);
                    data.opSetInput(op, secop->getIn(0), 0);
                    data.opSetInput(op, newvn, 1);
                }
                else
                {
                    newvn = data.newConstant(size, 0);
                    data.opSetOpcode(op, CPUI_COPY);
                    data.opSetInput(op, newvn, 0);
                    data.opRemoveInput(op, 1);
                }
            }
            else if (sa1 == sa2 && size <= sizeof(uintb))
            {   // FIXME:  precision
                mask = calc_mask(size);
                if (opc1 == CPUI_INT_LEFT)
                    mask = (mask << sa1) & mask;
                else
                    mask = (mask >> sa1) & mask;
                newvn = data.newConstant(size, mask);
                data.opSetOpcode(op, CPUI_INT_AND);
                data.opSetInput(op, secop->getIn(0), 0);
                data.opSetInput(op, newvn, 1);
            }
            else
                return 0;
            return 1;
        }
    }
}
