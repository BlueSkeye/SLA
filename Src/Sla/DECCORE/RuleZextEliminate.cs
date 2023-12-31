﻿using Sla.CORE;
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

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
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
        public override void getOpList(List<OpCode> oplist)
        {
            OpCode[] list = {OpCode.CPUI_INT_EQUAL, OpCode.CPUI_INT_NOTEQUAL,
                OpCode.CPUI_INT_LESS,OpCode.CPUI_INT_LESSEQUAL };
            oplist.AddRange(list);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp zext;
            Varnode vn1;
            Varnode vn2;
            Varnode newvn;
            ulong val;
            int smallsize, zextslot, otherslot;

            // vn1 equals ZEXTed input
            // vn2 = other input
            vn1 = op.getIn(0);
            vn2 = op.getIn(1);
            zextslot = 0;
            otherslot = 1;
            if ((vn2.isWritten()) && (vn2.getDef().code() == OpCode.CPUI_INT_ZEXT)) {
                vn1 = vn2;
                vn2 = op.getIn(0);
                zextslot = 1;
                otherslot = 0;
            }
            else if ((!vn1.isWritten()) || (vn1.getDef().code() != OpCode.CPUI_INT_ZEXT))
                return false;

            if (!vn2.isConstant()) return false;
            zext = vn1.getDef();
            if (!zext.getIn(0).isHeritageKnown()) return false;
            if (vn1.loneDescend() != op) return 0; // Make sure extension is not used for anything else
            smallsize = zext.getIn(0).getSize();
            val = vn2.getOffset();
            if ((val >> (8 * smallsize)) == 0) {
                // Is zero extension unnecessary
                newvn = data.newConstant(smallsize, val);
                newvn.copySymbolIfValid(vn2);
                data.opSetInput(op, zext.getIn(0), zextslot);
                data.opSetInput(op, newvn, otherslot);
                return true;
            }
            // Should have else for doing 
            // constant comparison here and now
            return false;
        }
    }
}
