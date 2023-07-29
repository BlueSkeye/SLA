using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleShiftCompare : Rule
    {
        public RuleShiftCompare(string g)
            : base(g, 0, "shiftcompare")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleShiftCompare(getGroup());
        }

        /// \class RuleShiftCompare
        /// \brief Transform shifts in comparisons:  `V >> c == d  =>  V == (d << c)`
        ///
        /// Similarly: `V << c == d  =>  V & mask == (d >> c)`
        ///
        /// The rule works on both INT_EQUAL and INT_NOTEQUAL.
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_EQUAL);
            oplist.push_back(CPUI_INT_NOTEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* shiftvn,*constvn,*savn,*mainvn;
            PcodeOp* shiftop;
            int sa;
            ulong constval, nzmask, newconst;
            OpCode opc;
            bool isleft;

            shiftvn = op.getIn(0);
            constvn = op.getIn(1);
            if (!constvn.isConstant()) return 0;
            if (!shiftvn.isWritten()) return 0;
            shiftop = shiftvn.getDef();
            opc = shiftop.code();
            if (opc == CPUI_INT_LEFT)
            {
                isleft = true;
                savn = shiftop.getIn(1);
                if (!savn.isConstant()) return 0;
                sa = savn.getOffset();
            }
            else if (opc == CPUI_INT_RIGHT)
            {
                isleft = false;
                savn = shiftop.getIn(1);
                if (!savn.isConstant()) return 0;
                sa = savn.getOffset();
                // There are definitely some situations where you don't want this rule to apply, like jump
                // table analysis where the switch variable is a bit field.
                // When shifting to the right, this is a likely shift out of a bitfield, which we would want to keep
                // We only apply when we know we will eliminate a variable
                if (shiftvn.loneDescend() != op) return 0;
            }
            else if (opc == CPUI_INT_MULT)
            {
                isleft = true;
                savn = shiftop.getIn(1);
                if (!savn.isConstant()) return 0;
                ulong val = savn.getOffset();
                sa = leastsigbit_set(val);
                if ((val >> sa) != (ulong)1) return 0; // Not multiplying by a power of 2
            }
            else if (opc == CPUI_INT_DIV)
            {
                isleft = false;
                savn = shiftop.getIn(1);
                if (!savn.isConstant()) return 0;
                ulong val = savn.getOffset();
                sa = leastsigbit_set(val);
                if ((val >> sa) != (ulong)1) return 0; // Not dividing by a power of 2
                if (shiftvn.loneDescend() != op) return 0;
            }
            else
                return 0;

            if (sa == 0) return 0;
            mainvn = shiftop.getIn(0);
            if (mainvn.isFree()) return 0;
            if (mainvn.getSize() > sizeof(ulong)) return 0;    // FIXME: ulong should be arbitrary precision

            constval = constvn.getOffset();
            nzmask = mainvn.getNZMask();
            if (isleft)
            {
                newconst = constval >> sa;
                if ((newconst << sa) != constval) return 0; // Information lost in constval
                ulong tmp = (nzmask << sa) & calc_mask(shiftvn.getSize());
                if ((tmp >> sa) != nzmask)
                {   // Information is lost in main
                    // We replace the LEFT with and AND mask
                    // This must be the lone use of the shift
                    if (shiftvn.loneDescend() != op) return 0;
                    sa = 8 * shiftvn.getSize() - sa;
                    tmp = (((ulong)1) << sa) - 1;
                    Varnode* newmask = data.newConstant(constvn.getSize(), tmp);
                    PcodeOp* newop = data.newOp(2, op.getAddr());
                    data.opSetOpcode(newop, CPUI_INT_AND);
                    Varnode* newtmpvn = data.newUniqueOut(constvn.getSize(), newop);
                    data.opSetInput(newop, mainvn, 0);
                    data.opSetInput(newop, newmask, 1);
                    data.opInsertBefore(newop, shiftop);
                    data.opSetInput(op, newtmpvn, 0);
                    data.opSetInput(op, data.newConstant(constvn.getSize(), newconst), 1);
                    return 1;
                }
            }
            else
            {
                if (((nzmask >> sa) << sa) != nzmask) return 0; // Information is lost
                newconst = (constval << sa) & calc_mask(shiftvn.getSize());
                if ((newconst >> sa) != constval) return 0; // Information is lost in constval
            }
            Varnode* newconstvn = data.newConstant(constvn.getSize(), newconst);
            data.opSetInput(op, mainvn, 0);
            data.opSetInput(op, newconstvn, 1);
            return 1;
        }
    }
}
