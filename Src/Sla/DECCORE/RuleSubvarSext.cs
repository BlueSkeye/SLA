using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSubvarSext : Rule
    {
        /// Is it guaranteed the root is a sub-variable needing to be trimmed
        private bool isaggressive;
        
        public RuleSubvarSext(string g)
            : base(g, 0, "subvar_sext")
        {
            isaggressive = false;
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubvarSext(getGroup());
        }

        /// \class RuleSubvarSext
        /// \brief Perform SubvariableFlow analysis triggered by INT_SEXT
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SEXT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn = op.getOut();
            Varnode invn = op.getIn(0);
            ulong mask = Globals.calc_mask((uint)invn.getSize());

            SubvariableFlow subflow = new SubvariableFlow(data, vn,
                mask, isaggressive, true, false);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }

        public override void reset(Funcdata data)
        {
            isaggressive = data.getArch().aggressive_ext_trim;
        }
    }
}
