using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleConcatShift : Rule
    {
        public RuleConcatShift(string g)
            : base(g, 0, "concatshift")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleConcatShift(getGroup());
        }

        /// \class RuleConcatShift
        /// \brief Simplify INT_RIGHT canceling PIECE: `concat(V,W) >> c  =>  zext(V)`
        ///
        /// Right shifts (signed and unsigned) can throw away the least significant part
        /// of a concatentation.  The result is a (sign or zero) extension of the most significant part.
        /// Depending on the original shift amount, the extension may still need to be shifted.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
            oplist.Add(OpCode.CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant()) return 0;

            Varnode shiftin = op.getIn(0);
            if (!shiftin.isWritten()) return 0;
            PcodeOp concat = shiftin.getDef();
            if (concat.code() != OpCode.CPUI_PIECE) return 0;

            int sa = op.getIn(1).getOffset();
            int leastsize = concat.getIn(1).getSize() * 8;
            if (sa < leastsize) return 0;   // Does shift throw away least sig part
            Varnode mainin = concat.getIn(0);
            if (mainin.isFree()) return 0;
            sa -= leastsize;
            OpCode extcode = (op.code() == OpCode.CPUI_INT_RIGHT) ? OpCode.CPUI_INT_ZEXT : OpCode.CPUI_INT_SEXT;
            if (sa == 0) {
                // Exact cancelation
                data.opRemoveInput(op, 1);  // Remove thrown away least
                data.opSetOpcode(op, extcode); // Change to extension
                data.opSetInput(op, mainin, 0);
            }
            else {
                // Create a new extension op
                PcodeOp extop = data.newOp(1, op.getAddr());
                data.opSetOpcode(extop, extcode);
                Varnode newvn = data.newUniqueOut(shiftin.getSize(), extop);
                data.opSetInput(extop, mainin, 0);

                // Adjust the shift amount
                data.opSetInput(op, newvn, 0);
                data.opSetInput(op, data.newConstant(op.getIn(1).getSize(), sa), 1);
                data.opInsertBefore(extop, op);
            }
            return 1;
        }
    }
}
