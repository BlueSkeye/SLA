
namespace Sla.DECCORE
{
    // \brief Make \e required Varnode merges as dictated by OpCode.CPUI_MULTIEQUAL,
    // OpCode.CPUI_INDIRECT, and \e addrtied property
    internal class ActionMergeRequired : Action
    {
        public ActionMergeRequired(string g)
            : base(ruleflags.rule_onceperfunc, "mergerequired", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeRequired(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeAddrTied();
            data.getMerge().groupPartials();
            data.getMerge().mergeMarker();
            return 0;
        }
    }
}
