
namespace Sla.DECCORE
{
    // \brief Try to merge Varnodes specified by Symbols with multiple SymbolEntrys
    internal class ActionMergeMultiEntry : Action
    {
        public ActionMergeMultiEntry(string g)
            : base(ruleflags.rule_onceperfunc, "mergemultientry", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeMultiEntry(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeMultiEntry();
            return 0;
        }
    }
}
