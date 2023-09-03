using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleDivChain : Rule
    {
        public RuleDivChain(string g)
            : base(g, 0, "divchain")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleDivChain(getGroup());
        }

        /// \class RuleDivChain
        /// \brief Collapse two consecutive divisions:  `(x / c1) / c2  =>  x / (c1*c2)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_DIV);
            oplist.Add(OpCode.CPUI_INT_SDIV);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            OpCode opc2 = op.code();
            Varnode constVn2 = op.getIn(1) ?? throw new ApplicationException();
            if (!constVn2.isConstant()) return 0;
            Varnode vn = op.getIn(0) ?? throw new ApplicationException();
            if (!vn.isWritten()) return 0;
            PcodeOp divOp = vn.getDef() ?? throw new ApplicationException();
            OpCode opc1 = divOp.code();
            if (opc1 != opc2 && (opc2 != OpCode.CPUI_INT_DIV || opc1 != OpCode.CPUI_INT_RIGHT))
                return 0;
            Varnode constVn1 = divOp.getIn(1) ?? throw new ApplicationException();
            if (!constVn1.isConstant()) return 0;
            // If the intermediate result is being used elsewhere, don't apply
            // Its likely collapsing the divisions will interfere with the modulo rules
            if (vn.loneDescend() == (PcodeOp)null) return 0;
            ulong val1;
            if (opc1 == opc2) {
                val1 = constVn1.getOffset();
            }
            else {
                // Unsigned case with INT_RIGHT
                int sa = (int)constVn1.getOffset();
                val1 = 1;
                val1 <<= sa;
            }
            Varnode baseVn = divOp.getIn(0) ?? throw new ApplicationException();
            if (baseVn.isFree()) return 0;
            int sz = vn.getSize();
            ulong val2 = constVn2.getOffset();
            ulong resval = (val1 * val2) & Globals.calc_mask((uint)sz);
            if (resval == 0) return 0;
            if (Globals.signbit_negative(val1, sz))
                val1 = (~val1 + 1) & Globals.calc_mask((uint)sz);
            if (Globals.signbit_negative(val2, sz))
                val2 = (~val2 + 1) & Globals.calc_mask((uint)sz);
            int bitcount = Globals.mostsigbit_set(val1) + Globals.mostsigbit_set(val2) + 2;
            if (opc2 == OpCode.CPUI_INT_DIV && bitcount > sz * 8)
                // Unsigned overflow
                return 0;
            if (opc2 == OpCode.CPUI_INT_SDIV && bitcount > sz * 8 - 2)
                // Signed overflow
                return 0;
            data.opSetInput(op, baseVn, 0);
            data.opSetInput(op, data.newConstant(sz, resval), 1);
            return 1;
        }
    }
}
