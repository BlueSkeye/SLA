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
    internal class RuleConcatLeftShift : Rule
    {
        public RuleConcatLeftShift(string g)
            : base(g, 0, "concatleftshift")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleConcatLeftShift(getGroup());
        }

        /// \class RuleConcatLeftShift
        /// \brief Simplify concatenation of extended value: `concat(V, zext(W) << c)  =>  concat( concat(V,W), 0)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_PIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn2 = op.getIn(1);
            if (!vn2.isWritten()) return 0;
            PcodeOp* shiftop = vn2.getDef();
            if (shiftop.code() != OpCode.CPUI_INT_LEFT) return 0;
            if (!shiftop.getIn(1).isConstant()) return 0; // Must be a constant shift
            int sa = shiftop.getIn(1).getOffset();
            if ((sa & 7) != 0) return 0;    // Not a multiple of 8
            Varnode* tmpvn = shiftop.getIn(0);
            if (!tmpvn.isWritten()) return 0;
            PcodeOp* zextop = tmpvn.getDef();
            if (zextop.code() != OpCode.CPUI_INT_ZEXT) return 0;
            Varnode* b = zextop.getIn(0);
            if (b.isFree()) return 0;
            Varnode* vn1 = op.getIn(0);
            if (vn1.isFree()) return 0;
            sa /= 8;            // bits to bytes
            if (sa + b.getSize() != tmpvn.getSize()) return 0; // Must shift to most sig boundary

            PcodeOp* newop = data.newOp(2, op.getAddr());
            data.opSetOpcode(newop, OpCode.CPUI_PIECE);
            Varnode* newout = data.newUniqueOut(vn1.getSize() + b.getSize(), newop);
            data.opSetInput(newop, vn1, 0);
            data.opSetInput(newop, b, 1);
            data.opInsertBefore(newop, op);
            data.opSetInput(op, newout, 0);
            data.opSetInput(op, data.newConstant(op.getOut().getSize() - newout.getSize(), 0), 1);
            return 1;
        }
    }
}
