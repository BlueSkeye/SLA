using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSubvarSubpiece : Rule
    {
        public RuleSubvarSubpiece(string g)
            : base(g, 0, "subvar_subpiece")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubvarSubpiece(getGroup());
        }

        /// \class RuleSubvarSubpiece
        /// \brief Perform SubVariableFlow analysis triggered by SUBPIECE
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn = op.getIn(0);
            Varnode outvn = op.getOut();
            int flowsize = outvn.getSize();
            ulong mask = Globals.calc_mask((uint)flowsize);
            mask <<= 8 * ((int)op.getIn(1).getOffset());
            bool aggressive = outvn.isPtrFlow();
            if (!aggressive) {
                if ((vn.getConsume() & mask) != vn.getConsume()) return 0;
                if (op.getOut().hasNoDescend()) return 0;
            }
            bool big = false;
            if (flowsize >= 8 && vn.isInput()) {
                // Vector register inputs getting truncated to what actually gets used
                // happens occasionally.  We let SubvariableFlow deal with this special case
                // to avoid overlapping inputs
                // TODO: ActionLaneDivide should be handling this
                if (vn.loneDescend() == op)
                    big = true;
            }
            SubvariableFlow subflow = new SubvariableFlow(data, vn, mask, aggressive, false, big);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }
    }
}
