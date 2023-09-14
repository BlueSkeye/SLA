
namespace Sla.DECCORE
{
    /// \brief Replace COPYs from the same source with a single dominant COPY
    internal class ActionDominantCopy : Action
    {
        public ActionDominantCopy(string g)
            : base(ruleflags.rule_onceperfunc,"dominantcopy", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDominantCopy(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().processCopyTrims();
            return 0;
        }
    }
}
