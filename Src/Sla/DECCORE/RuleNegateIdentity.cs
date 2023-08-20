using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleNegateIdentity : Rule
    {
        public RuleNegateIdentity(string g)
            : base(g, 0, "negateidentity")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return !grouplist.contains(getGroup()) ? (Rule)null : new RuleNegateIdentity(getGroup());
        }

        /// \class RuleNegateIdentity
        /// \brief Apply INT_NEGATE identities:  `V & ~V  => #0,  V | ~V  .  #-1`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_NEGATE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn = op.getIn(0);
            Varnode outVn = op.getOut();
            IEnumerator<PcodeOp> iter = outVn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp logicOp = iter.Current;
                OpCode opc = logicOp.code();
                if (opc != OpCode.CPUI_INT_AND && opc != OpCode.CPUI_INT_OR && opc != OpCode.CPUI_INT_XOR)
                    continue;
                int slot = logicOp.getSlot(outVn);
                if (logicOp.getIn(1 - slot) != vn) continue;
                ulong value = 0;
                if (opc != OpCode.CPUI_INT_AND)
                    value = Globals.calc_mask((uint)vn.getSize());
                data.opSetInput(logicOp, data.newConstant(vn.getSize(), value), 0);
                data.opRemoveInput(logicOp, 1);
                data.opSetOpcode(logicOp, OpCode.CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
