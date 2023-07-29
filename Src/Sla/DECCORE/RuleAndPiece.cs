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
    internal class RuleAndPiece : Rule
    {
        public RuleAndPiece(string g)
            : base(g, 0, "andpiece")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleAndPiece(getGroup());
        }

        /// \class RuleAndPiece
        /// \brief Convert PIECE to INT_ZEXT where appropriate: `V & concat(W,X)  =>  zext(X)`
        ///
        /// Conversion to INT_ZEXT works if we know the upper part of the result is zero.
        ///
        /// Similarly if the lower part is zero:  `V & concat(W,X)  =>  V & concat(#0,X)`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_AND);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode piecevn;
            Varnode othervn;
            Varnode highvn;
            Varnode lowvn;
            Varnode newvn;
            Varnode newvn2;
            PcodeOp pieceop;
            PcodeOp newop;
            ulong othermask, maskhigh, masklow;
            OpCode opc = CPUI_PIECE;    // Unnecessary initialization
            int i, size;

            size = op.getOut().getSize();
            highvn = lowvn = (Varnode)null; // Unnecessary initialization
            for (i = 0; i < 2; ++i)
            {
                piecevn = op.getIn(i);
                if (!piecevn.isWritten()) continue;
                pieceop = piecevn.getDef();
                if (pieceop.code() != CPUI_PIECE) continue;
                othervn = op.getIn(1 - i);
                othermask = othervn.getNZMask();
                if (othermask == Globals.calc_mask(size)) continue;
                if (othermask == 0) continue; // Handled by andmask
                highvn = pieceop.getIn(0);
                if (!highvn.isHeritageKnown()) continue;
                lowvn = pieceop.getIn(1);
                if (!lowvn.isHeritageKnown()) continue;
                maskhigh = highvn.getNZMask();
                masklow = lowvn.getNZMask();
                if ((maskhigh & (othermask >> (lowvn.getSize() * 8))) == 0)
                {
                    if ((maskhigh == 0) && (highvn.isConstant())) continue; // Handled by piece2zext
                    opc = CPUI_INT_ZEXT;
                    break;
                }
                else if ((masklow & othermask) == 0)
                {
                    if (lowvn.isConstant()) continue; // Nothing to do
                    opc = CPUI_PIECE;
                    break;
                }
            }
            if (i == 2) return 0;
            if (opc == CPUI_INT_ZEXT)
            {   // Change PIECE(a,b) to ZEXT(b)
                newop = data.newOp(1, op.getAddr());
                data.opSetOpcode(newop, opc);
                data.opSetInput(newop, lowvn, 0);
            }
            else
            {           // Change PIECE(a,b) to PIECE(a,#0)
                newvn2 = data.newConstant(lowvn.getSize(), 0);
                newop = data.newOp(2, op.getAddr());
                data.opSetOpcode(newop, opc);
                data.opSetInput(newop, highvn, 0);
                data.opSetInput(newop, newvn2, 1);
            }
            newvn = data.newUniqueOut(size, newop);
            data.opInsertBefore(newop, op);
            data.opSetInput(op, newvn, i);
            return 1;
        }
    }
}
