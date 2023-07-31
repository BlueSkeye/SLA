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
    internal class RuleZextShiftZext : Rule
    {
        public RuleZextShiftZext(string g)
            : base(g, 0, "zextshiftzext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleZextShiftZext(getGroup());
        }

        /// \class RuleZextShiftZext
        /// \brief Simplify multiple INT_ZEXT operations: `zext( zext(V) << c )  => zext(V) << c`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_ZEXT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* invn = op.getIn(0);
            if (!invn.isWritten()) return 0;
            PcodeOp* shiftop = invn.getDef();
            if (shiftop.code() == OpCode.CPUI_INT_ZEXT)
            {   // Check for ZEXT( ZEXT( a ) )
                Varnode* vn = shiftop.getIn(0);
                if (vn.isFree()) return 0;
                if (invn.loneDescend() != op)      // Only propagate if -op- is only use of -invn-
                    return 0;
                data.opSetInput(op, vn, 0);
                return 1;
            }
            if (shiftop.code() != OpCode.CPUI_INT_LEFT) return 0;
            if (!shiftop.getIn(1).isConstant()) return 0;
            if (!shiftop.getIn(0).isWritten()) return 0;
            PcodeOp* zext2op = shiftop.getIn(0).getDef();
            if (zext2op.code() != OpCode.CPUI_INT_ZEXT) return 0;
            Varnode* rootvn = zext2op.getIn(0);
            if (rootvn.isFree()) return 0;

            ulong sa = shiftop.getIn(1).getOffset();
            if (sa > 8 * (ulong)(zext2op.getOut().getSize() - rootvn.getSize()))
                return 0; // Shift might lose bits off the top
            PcodeOp* newop = data.newOp(1, op.getAddr());
            data.opSetOpcode(newop, OpCode.CPUI_INT_ZEXT);
            Varnode* outvn = data.newUniqueOut(op.getOut().getSize(), newop);
            data.opSetInput(newop, rootvn, 0);
            data.opSetOpcode(op, OpCode.CPUI_INT_LEFT);
            data.opSetInput(op, outvn, 0);
            data.opInsertInput(op, data.newConstant(4, sa), 1);
            data.opInsertBefore(newop, op);
            return 1;
        }
    }
}
