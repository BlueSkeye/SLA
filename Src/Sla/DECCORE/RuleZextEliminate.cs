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
    internal class RuleZextEliminate : Rule
    {
        public RuleZextEliminate(string g)
            : base(g, 0, "zexteliminate")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleZextEliminate(getGroup());
        }

        /// \class RuleZextEliminate
        /// \brief Eliminate INT_ZEXT in comparisons:  `zext(V) == c  =>  V == c`
        ///
        /// The constant Varnode changes size and must not lose any non-zero bits.
        /// Handle other variants with INT_NOTEQUAL, INT_LESS, and INT_LESSEQUAL
        ///   - `zext(V) != c =>  V != c`
        ///   - `zext(V) < c  =>  V < c`
        ///   - `zext(V) <= c =>  V <= c`
        public override void getOpList(List<uint4> oplist)
        {
            uint4 list[] = {CPUI_INT_EQUAL, CPUI_INT_NOTEQUAL,
          CPUI_INT_LESS,CPUI_INT_LESSEQUAL };
            oplist.insert(oplist.end(), list, list + 4);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* zext;
            Varnode* vn1,*vn2,*newvn;
            uintb val;
            int4 smallsize, zextslot, otherslot;

            // vn1 equals ZEXTed input
            // vn2 = other input
            vn1 = op.getIn(0);
            vn2 = op.getIn(1);
            zextslot = 0;
            otherslot = 1;
            if ((vn2.isWritten()) && (vn2.getDef().code() == CPUI_INT_ZEXT))
            {
                vn1 = vn2;
                vn2 = op.getIn(0);
                zextslot = 1;
                otherslot = 0;
            }
            else if ((!vn1.isWritten()) || (vn1.getDef().code() != CPUI_INT_ZEXT))
                return 0;

            if (!vn2.isConstant()) return 0;
            zext = vn1.getDef();
            if (!zext.getIn(0).isHeritageKnown()) return 0;
            if (vn1.loneDescend() != op) return 0; // Make sure extension is not used for anything else
            smallsize = zext.getIn(0).getSize();
            val = vn2.getOffset();
            if ((val >> (8 * smallsize)) == 0)
            { // Is zero extension unnecessary
                newvn = data.newConstant(smallsize, val);
                newvn.copySymbolIfValid(vn2);
                data.opSetInput(op, zext.getIn(0), zextslot);
                data.opSetInput(op, newvn, otherslot);
                return 1;
            }
            // Should have else for doing 
            // constant comparison here and now
            return 0;
        }
    }
}
