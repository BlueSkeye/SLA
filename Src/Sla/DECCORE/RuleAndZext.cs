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
    internal class RuleAndZext : Rule
    {
        public RuleAndZext(string g)
            : base(g, 0, "andzext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleAndZext(getGroup());
        }

        /// \class RuleAndZext
        /// \brief Convert INT_AND to INT_ZEXT where appropriate: `sext(X) & 0xffff  =>  zext(X)`
        ///
        /// Similarly `concat(Y,X) & 0xffff  =>  zext(X)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_AND);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* cvn1 = op.getIn(1);
            if (!cvn1.isConstant()) return 0;
            if (!op.getIn(0).isWritten()) return 0;
            PcodeOp* otherop = op.getIn(0).getDef();
            OpCode opc = otherop.code();
            Varnode* rootvn;
            if (opc == OpCode.CPUI_INT_SEXT)
                rootvn = otherop.getIn(0);
            else if (opc == OpCode.CPUI_PIECE)
                rootvn = otherop.getIn(1);
            else
                return 0;
            ulong mask = Globals.calc_mask(rootvn.getSize());
            if (mask != cvn1.getOffset())
                return 0;
            if (rootvn.isFree())
                return 0;
            if (rootvn.getSize() > sizeof(ulong))  // FIXME: Should be arbitrary precision
                return 0;
            data.opSetOpcode(op, OpCode.CPUI_INT_ZEXT);
            data.opRemoveInput(op, 1);
            data.opSetInput(op, rootvn, 0);
            return 1;
        }
    }
}
