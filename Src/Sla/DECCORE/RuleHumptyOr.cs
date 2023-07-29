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
    internal class RuleHumptyOr : Rule
    {
        public RuleHumptyOr(string g)
            : base(g, 0, "humptyor")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleHumptyOr(getGroup());
        }

        /// \class RuleHumptyOr
        /// \brief Simplify masked pieces INT_ORed together:  `(V & ff00) | (V & 00ff)  =>  V`
        ///
        /// This supports the more general form:
        ///  - `(V & W) | (V & X)  =>  V & (W|X)`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_OR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn1,*vn2;
            Varnode* a, *b, *c, *d;
            PcodeOp* and1,*and2;

            vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            vn2 = op.getIn(1);
            if (!vn2.isWritten()) return 0;
            and1 = vn1.getDef();
            if (and1.code() != CPUI_INT_AND) return 0;
            and2 = vn2.getDef();
            if (and2.code() != CPUI_INT_AND) return 0;
            a = and1.getIn(0);
            b = and1.getIn(1);
            c = and2.getIn(0);
            d = and2.getIn(1);
            if (a == c)
            {
                c = d;      // non-matching are b and d
            }
            else if (a == d)
            {   // non-matching are b and c
            }
            else if (b == c)
            {   // non-matching are a and d
                b = a;
                a = c;
                c = d;
            }
            else if (b == d)
            {   // non-matching are a and c
                b = a;
                a = d;
            }
            else
                return 0;
            // Reaching here a, matches across both ANDs, b and c are the respective other params
            // We know a is not free, because there are at least two references to it
            if (b.isConstant() && c.isConstant())
            {
                ulong totalbits = b.getOffset() | c.getOffset();
                if (totalbits == calc_mask(a.getSize()))
                {
                    // Between the two sides, we get all bits of a. Convert to COPY
                    data.opSetOpcode(op, CPUI_COPY);
                    data.opRemoveInput(op, 1);
                    data.opSetInput(op, a, 0);
                }
                else
                {
                    // We get some bits, but not all.  Convert to an AND
                    data.opSetOpcode(op, CPUI_INT_AND);
                    data.opSetInput(op, a, 0);
                    Varnode* newconst = data.newConstant(a.getSize(), totalbits);
                    data.opSetInput(op, newconst, 1);
                }
            }
            else
            {
                if (!b.isHeritageKnown()) return 0;
                if (!c.isHeritageKnown()) return 0;
                ulong aMask = a.getNZMask();
                if ((b.getNZMask() & aMask) == 0) return 0; // RuleAndDistribute would reverse us
                if ((c.getNZMask() & aMask) == 0) return 0; // RuleAndDistribute would reverse us
                PcodeOp* newOrOp = data.newOp(2, op.getAddr());
                data.opSetOpcode(newOrOp, CPUI_INT_OR);
                Varnode* orVn = data.newUniqueOut(a.getSize(), newOrOp);
                data.opSetInput(newOrOp, b, 0);
                data.opSetInput(newOrOp, c, 1);
                data.opInsertBefore(newOrOp, op);
                data.opSetInput(op, a, 0);
                data.opSetInput(op, orVn, 1);
                data.opSetOpcode(op, CPUI_INT_AND);
            }
            return 1;
        }
    }
}
