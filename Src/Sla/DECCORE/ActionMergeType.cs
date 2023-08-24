
namespace Sla.DECCORE
{
    /// \brief Try to merge Varnodes of the same type (if they don't hold different values at the same time)
    internal class ActionMergeType : Action
    {
        public ActionMergeType(string g)
            : base(ruleflags.rule_onceperfunc, "mergetype", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeType(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeByDatatype(data.beginLoc() /*, data.endLoc()*/);
            return 0;
        }
    }
}
