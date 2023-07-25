using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubZext : Rule
    {
        public RuleSubZext(string g)
            : base(g, 0, "subzext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubZext(getGroup());
        }

        /// \class RuleSubZext
        /// \brief Simplify INT_ZEXT applied to SUBPIECE expressions
        ///
        /// This performs:
        ///  - `zext( sub( V, 0) )        =>    V & mask`
        ///  - `zext( sub( V, c)          =>    (V >> c*8) & mask`
        ///  - `zext( sub( V, c) >> d )   =>    (V >> (c*8+d)) & mask`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_ZEXT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* subvn,*basevn,*constvn;
            PcodeOp* subop;
            uintb val;

            subvn = op->getIn(0);
            if (!subvn->isWritten()) return 0;
            subop = subvn->getDef();
            if (subop->code() == CPUI_SUBPIECE)
            {
                basevn = subop->getIn(0);
                if (basevn->isFree()) return 0;
                if (basevn->getSize() != op->getOut()->getSize()) return 0; // Truncating then extending to same size
                if (basevn->getSize() > sizeof(uintb))
                    return 0;
                if (subop->getIn(1)->getOffset() != 0)
                { // If truncating from middle
                    if (subvn->loneDescend() != op) return 0; // and there is no other use of the truncated value
                    Varnode* newvn = data.newUnique(basevn->getSize(), (Datatype*)0);
                    constvn = subop->getIn(1);
                    uintb rightVal = constvn->getOffset() * 8;
                    data.opSetInput(op, newvn, 0);
                    data.opSetOpcode(subop, CPUI_INT_RIGHT); // Convert the truncation to a shift
                    data.opSetInput(subop, data.newConstant(constvn->getSize(), rightVal), 1);
                    data.opSetOutput(subop, newvn);
                }
                else
                    data.opSetInput(op, basevn, 0); // Otherwise, bypass the truncation entirely
                val = calc_mask(subvn->getSize());
                constvn = data.newConstant(basevn->getSize(), val);
                data.opSetOpcode(op, CPUI_INT_AND);
                data.opInsertInput(op, constvn, 1);
                return 1;
            }
            else if (subop->code() == CPUI_INT_RIGHT)
            {
                PcodeOp* shiftop = subop;
                if (!shiftop->getIn(1)->isConstant()) return 0;
                Varnode* midvn = shiftop->getIn(0);
                if (!midvn->isWritten()) return 0;
                subop = midvn->getDef();
                if (subop->code() != CPUI_SUBPIECE) return 0;
                basevn = subop->getIn(0);
                if (basevn->isFree()) return 0;
                if (basevn->getSize() != op->getOut()->getSize()) return 0; // Truncating then extending to same size
                if (midvn->loneDescend() != shiftop) return 0;
                if (subvn->loneDescend() != op) return 0;
                val = calc_mask(midvn->getSize()); // Mask based on truncated size
                uintb sa = shiftop->getIn(1)->getOffset(); // The shift shrinks the mask even further
                val >>= sa;
                sa += subop->getIn(1)->getOffset() * 8; // The total shift = truncation + small shift
                Varnode* newvn = data.newUnique(basevn->getSize(), (Datatype*)0);
                data.opSetInput(op, newvn, 0);
                data.opSetInput(shiftop, basevn, 0); // Shift the full value, instead of the truncated value
                data.opSetInput(shiftop, data.newConstant(shiftop->getIn(1)->getSize(), sa), 1);    // by the combined amount
                data.opSetOutput(shiftop, newvn);
                constvn = data.newConstant(basevn->getSize(), val);
                data.opSetOpcode(op, CPUI_INT_AND); // Turn the ZEXT into an AND
                data.opInsertInput(op, constvn, 1); // With the appropriate mask
                return 1;
            }
            return 0;
        }
    }
}
