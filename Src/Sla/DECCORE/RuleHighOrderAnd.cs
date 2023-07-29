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
    internal class RuleHighOrderAnd : Rule
    {
        public RuleHighOrderAnd(string g)
            : base(g, 0, "highorderand")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleHighOrderAnd(getGroup());
        }

        /// \class RuleHighOrderAnd
        /// \brief Simplify INT_AND when applied to aligned INT_ADD:  `(V + c) & 0xfff0  =>  V + (c & 0xfff0)`
        ///
        /// If V and W are aligned to a mask, then
        /// `((V + c) + W) & 0xfff0   =>   (V + (c & 0xfff0)) + W`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_AND);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* xalign;
            Varnode* cvn1 = op->getIn(1);
            if (!cvn1->isConstant()) return 0;
            if (!op->getIn(0)->isWritten()) return 0;
            PcodeOp* addop = op->getIn(0)->getDef();
            if (addop->code() != CPUI_INT_ADD) return 0;

            uintb val = cvn1->getOffset();
            int4 size = cvn1->getSize();
            // Check that cvn1 is of the form    11110000
            if (((val - 1) | val) != calc_mask(size)) return 0;

            Varnode* cvn2 = addop->getIn(1);
            if (cvn2->isConstant())
            {
                xalign = addop->getIn(0);
                if (xalign->isFree()) return 0;
                uintb mask1 = xalign->getNZMask();
                // addop->Input(0) must be unaffected by the AND
                if ((mask1 & val) != mask1) return 0;

                data.opSetOpcode(op, CPUI_INT_ADD);
                data.opSetInput(op, xalign, 0);
                val = val & cvn2->getOffset();
                data.opSetInput(op, data.newConstant(size, val), 1);
                return 1;
            }
            else
            {
                if (addop->getOut()->loneDescend() != op) return 0;
                for (int4 i = 0; i < 2; ++i)
                {
                    Varnode* zerovn = addop->getIn(i);
                    uintb mask2 = zerovn->getNZMask();
                    if ((mask2 & val) != mask2) continue; // zerovn must be unaffected by the AND operation
                    Varnode* nonzerovn = addop->getIn(1 - i);
                    if (!nonzerovn->isWritten()) continue;
                    PcodeOp* addop2 = nonzerovn->getDef();
                    if (addop2->code() != CPUI_INT_ADD) continue;
                    if (nonzerovn->loneDescend() != addop) continue;
                    cvn2 = addop2->getIn(1);
                    if (!cvn2->isConstant()) continue;
                    xalign = addop2->getIn(0);
                    mask2 = xalign->getNZMask();
                    if ((mask2 & val) != mask2) continue;
                    val = val & cvn2->getOffset();
                    data.opSetInput(addop2, data.newConstant(size, val), 1);
                    // Convert the AND to a COPY
                    data.opRemoveInput(op, 1);
                    data.opSetOpcode(op, CPUI_COPY);
                    return 1;
                }
            }
            return 0;
        }
    }
}
