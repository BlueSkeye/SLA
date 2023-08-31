using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleIntLessEqual : Rule
    {
        public RuleIntLessEqual(string g)
            : base(g, 0, "intlessequal")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleIntLessEqual(getGroup());
        }

        /// \class RuleIntLessEqual
        /// \brief Convert LESSEQUAL to LESS:  `V <= c  =>  V < (c+1)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_LESSEQUAL);
            oplist.Add(OpCode.CPUI_INT_SLESSEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (data.replaceLessequal(op))
                return 1;
            return 0;
        }
    }
}
