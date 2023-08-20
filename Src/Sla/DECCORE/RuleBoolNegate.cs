using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleBoolNegate : Rule
    {
        public RuleBoolNegate(string g)
            : base(g, 0, "boolnegate")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RuleBoolNegate(getGroup());
        }

        /// \class RuleBoolNegate
        /// \brief Apply a set of identities involving BOOL_NEGATE
        ///
        /// The identities include:
        ///  - `!!V  =>  V`
        ///  - `!(V == W)  =>  V != W`
        ///  - `!(V < W)   =>  W <= V`
        ///  - `!(V <= W)  =>  W < V`
        ///  - `!(V != W)  =>  V == W`
        ///
        /// This supports signed and floating-point variants as well
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_BOOL_NEGATE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            OpCode opc;

            Varnode vn = op.getIn(0);
            if (!vn.isWritten()) return 0;
            PcodeOp flip_op = vn.getDef() ?? throw new ApplicationException();

            IEnumerator<PcodeOp> iter = vn.beginDescend();

            // ALL descendants must be negates
            while (iter.MoveNext())
                if (iter.Current.code() != OpCode.CPUI_BOOL_NEGATE) return 0;

            bool flipyes;
            opc = Globals.get_booleanflip(flip_op.code(), out flipyes);
            if (opc == OpCode.CPUI_MAX) return 0;
            data.opSetOpcode(flip_op, opc); // Set the negated opcode
            if (flipyes)
                // Do we need to reverse the two operands
                data.opSwapInput(flip_op, 0, 1);
            iter = vn.beginDescend();
            while (iter.MoveNext())
                // Remove all the negates
                data.opSetOpcode(iter.Current, OpCode.CPUI_COPY);
            return 1;
        }
    }
}
