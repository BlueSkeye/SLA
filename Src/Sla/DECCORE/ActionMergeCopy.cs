
namespace Sla.DECCORE
{
    // \brief Try to merge the input and output Varnodes of a OpCode.CPUI_COPY op
    internal class ActionMergeCopy : Action
    {
        public ActionMergeCopy(string g)
            : base(ruleflags.rule_onceperfunc, "mergecopy", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeCopy(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeOpcode(CORE.OpCode.CPUI_COPY);
            return 0;
        }
    }
}
