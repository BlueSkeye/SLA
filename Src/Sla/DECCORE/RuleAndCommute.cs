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
    internal class RuleAndCommute : Rule
    {
        public RuleAndCommute(string g)
            : base(g, 0, "andcommute")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleAndCommute(getGroup());
        }

        /// \class RuleAndCommute
        /// \brief Commute INT_AND with INT_LEFT and INT_RIGHT: `(V << W) & d  =>  (V & (W >> c)) << c`
        ///
        /// This makes sense to do if W is constant and there is no other use of (V << W)
        /// If W is \b not constant, it only makes sense if the INT_AND is likely to cancel
        /// with a specific INT_OR or PIECE
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_AND);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* orvn,*shiftvn,*othervn,*newvn1,*newvn2,*savn;
            PcodeOp* orop,*shiftop,*newop1,*newop2;
            ulong ormask1, ormask2, othermask, fullmask;
            OpCode opc = CPUI_INT_OR; // Unnecessary initialization
            int sa, i, size;

            orvn = othervn = savn = (Varnode*)0; // Unnecessary initialization
            size = op.getOut().getSize();
            if (size > sizeof(ulong)) return 0; // FIXME: ulong should be arbitrary precision
            fullmask = calc_mask(size);
            for (i = 0; i < 2; ++i)
            {
                shiftvn = op.getIn(i);
                shiftop = shiftvn.getDef();
                if (shiftop == (PcodeOp*)0) continue;
                opc = shiftop.code();
                if ((opc != CPUI_INT_LEFT) && (opc != CPUI_INT_RIGHT)) continue;
                savn = shiftop.getIn(1);
                if (!savn.isConstant()) continue;
                sa = (int)savn.getOffset();

                othervn = op.getIn(1 - i);
                if (!othervn.isHeritageKnown()) continue;
                othermask = othervn.getNZMask();
                // Check if AND is only zeroing bits which are already
                // zeroed by the shift, in which case andmask takes
                // care of it
                if (opc == CPUI_INT_RIGHT)
                {
                    if ((fullmask >> sa) == othermask) continue;
                    othermask <<= sa;       // Calc mask as it will be after commute
                }
                else
                {
                    if (((fullmask << sa) && fullmask) == othermask) continue;
                    othermask >>= sa;       // Calc mask as it will be after commute
                }
                if (othermask == 0) continue; // Handled by andmask
                if (othermask == fullmask) continue;

                orvn = shiftop.getIn(0);
                if ((opc == CPUI_INT_LEFT) && (othervn.isConstant()))
                {
                    //  (v & #c) << #sa     if preferred to (v << #sa) & #(c << sa)
                    // because the mask is right/least justified, so it makes sense as a normalization
                    // NOTE: if the shift is right(>>) then performing the AND first does NOT give a justified mask
                    // NOTE: if we don't check that AND is masking with a constant, RuleAndCommute causes an infinite
                    //       sequence of transforms
                    if (shiftvn.loneDescend() == op) break; // If there is no other use of shift, always do the commute
                }

                if (!orvn.isWritten()) continue;
                orop = orvn.getDef();

                if (orop.code() == CPUI_INT_OR)
                {
                    ormask1 = orop.getIn(0).getNZMask();
                    if ((ormask1 & othermask) == 0) break;
                    ormask2 = orop.getIn(1).getNZMask();
                    if ((ormask2 & othermask) == 0) break;
                    if (othervn.isConstant())
                    {
                        if ((ormask1 & othermask) == ormask1) break;
                        if ((ormask2 & othermask) == ormask2) break;
                    }
                }
                else if (orop.code() == CPUI_PIECE)
                {
                    ormask1 = orop.getIn(1).getNZMask();  // Low part of piece
                    if ((ormask1 & othermask) == 0) break;
                    ormask2 = orop.getIn(0).getNZMask();  // High part
                    ormask2 <<= orop.getIn(1).getSize() * 8;
                    if ((ormask2 & othermask) == 0) break;
                }
                else
                    continue;
            }
            if (i == 2) return 0;
            // Do the commute
            newop1 = data.newOp(2, op.getAddr());
            newvn1 = data.newUniqueOut(size, newop1);
            data.opSetOpcode(newop1, (opc == CPUI_INT_LEFT) ? CPUI_INT_RIGHT : CPUI_INT_LEFT);
            data.opSetInput(newop1, othervn, 0);
            data.opSetInput(newop1, savn, 1);
            data.opInsertBefore(newop1, op);

            newop2 = data.newOp(2, op.getAddr());
            newvn2 = data.newUniqueOut(size, newop2);
            data.opSetOpcode(newop2, CPUI_INT_AND);
            data.opSetInput(newop2, orvn, 0);
            data.opSetInput(newop2, newvn1, 1);
            data.opInsertBefore(newop2, op);

            data.opSetInput(op, newvn2, 0);
            data.opSetInput(op, savn, 1);
            data.opSetOpcode(op, opc);

            return 1;
        }
    }
}
