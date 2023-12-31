﻿using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleShiftPiece : Rule
    {
        public RuleShiftPiece(string g)
            : base(g, 0, "shiftpiece")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleShiftPiece(getGroup());
        }

        /// \class RuleShiftPiece
        /// \brief Convert "shift and add" to PIECE:  (zext(V) << 16) + zext(W)  =>  concat(V,W)
        ///
        /// The \e add operation can be INT_ADD, INT_OR, or INT_XOR. If the extension size is bigger
        /// than the concatenation size, the concatenation can be zero extended.
        /// This also supports other special forms where a value gets
        /// concatenated with its own sign extension bits.
        ///
        ///  - `(zext(V s>> 0x1f) << 0x20) + zext(V)  =>  sext(V)`
        ///  - `(zext(W s>> 0x1f) << 0x20) + X        =>  sext(W) where W = sub(X,0)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_OR);
            oplist.Add(OpCode.CPUI_INT_XOR);
            oplist.Add(OpCode.CPUI_INT_ADD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn1;
            Varnode vn2;

            vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            vn2 = op.getIn(1);
            if (!vn2.isWritten()) return 0;
            PcodeOp shiftop = vn1.getDef() ?? throw new BugException();
            PcodeOp zextloop = vn2.getDef() ?? throw new BugException();
            if (shiftop.code() != OpCode.CPUI_INT_LEFT) {
                if (zextloop.code() != OpCode.CPUI_INT_LEFT) return 0;
                PcodeOp tmpop = zextloop;
                zextloop = shiftop;
                shiftop = tmpop;
            }
            if (!shiftop.getIn(1).isConstant()) return 0;
            vn1 = shiftop.getIn(0);
            if (!vn1.isWritten()) return 0;
            PcodeOp zexthiop = vn1.getDef() ?? throw new BugException();
            if ((zexthiop.code() != OpCode.CPUI_INT_ZEXT) &&
                (zexthiop.code() != OpCode.CPUI_INT_SEXT))
                return 0;
            vn1 = zexthiop.getIn(0);
            if (vn1.isConstant()) {
                if (vn1.getSize() < sizeof(ulong))
                    return 0;       // Normally we let ZEXT of a constant collapse naturally
                                    // But if the ZEXTed constant is too big, this won't happen
            }
            else if (vn1.isFree())
                return 0;
            int sa = (int)shiftop.getIn(1).getOffset();
            int concatsize = sa + 8 * vn1.getSize();
            if (op.getOut().getSize() * 8 < concatsize) return 0;
            if (zextloop.code() != OpCode.CPUI_INT_ZEXT) {
                // This is a special case triggered by CDQ: IDIV
                // This would be handled by the base case, but it interacts with RuleSubZext sometimes
                if (!vn1.isWritten()) return 0;
                // Look for s<< #c forming the high piece
                PcodeOp? rShiftOp = vn1.getDef() ?? throw new BugException();
                if (rShiftOp.code() != OpCode.CPUI_INT_SRIGHT) return 0;
                if (!rShiftOp.getIn(1).isConstant()) return 0;
                vn2 = rShiftOp.getIn(0);
                if (!vn2.isWritten()) return 0;
                PcodeOp? subop = vn2.getDef() ?? throw new BugException();
                if (subop.code() != OpCode.CPUI_SUBPIECE) return 0;   // SUBPIECE connects high and low parts
                if (subop.getIn(1).getOffset() != 0) return 0;    //    (must be low part)
                Varnode bigVn = zextloop.getOut();
                if (subop.getIn(0) != bigVn) return 0; // Verify we have link thru SUBPIECE with low part
                int rsa = (int)rShiftOp.getIn(1).getOffset();
                if (rsa != vn2.getSize() * 8 - 1) return 0;    // Arithmetic shift must copy sign-bit thru whole high part
                if ((bigVn.getNZMask() >> sa) != 0) return 0;  // The original most significant bytes must be zero
                if (sa != 8 * (vn2.getSize())) return 0;
                data.opSetOpcode(op, OpCode.CPUI_INT_SEXT);        // Original op is simply a sign extension of low part
                data.opSetInput(op, vn2, 0);
                data.opRemoveInput(op, 1);
                return 1;
            }
            vn2 = zextloop.getIn(0);
            if (vn2.isFree()) return 0;
            if (sa != 8 * (vn2.getSize())) return 0;
            if (concatsize == op.getOut().getSize() * 8)
            {
                data.opSetOpcode(op, OpCode.CPUI_PIECE);
                data.opSetInput(op, vn1, 0);
                data.opSetInput(op, vn2, 1);
            }
            else
            {
                PcodeOp newop = data.newOp(2, op.getAddr());
                data.newUniqueOut(concatsize / 8, newop);
                data.opSetOpcode(newop, OpCode.CPUI_PIECE);
                data.opSetInput(newop, vn1, 0);
                data.opSetInput(newop, vn2, 1);
                data.opInsertBefore(newop, op);
                data.opSetOpcode(op, zexthiop.code());
                data.opRemoveInput(op, 1);
                data.opSetInput(op, newop.getOut(), 0);
            }
            return 1;
        }
    }
}
