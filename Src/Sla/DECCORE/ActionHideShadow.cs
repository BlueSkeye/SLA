
namespace Sla.DECCORE
{
    /// \brief Locate \e shadow Varnodes and adjust them so they are hidden
    ///
    /// A \b shadow Varnode is an internal copy of another Varnode that a compiler
    /// produces but that really isn't a separate variable.  In practice, a Varnode
    /// and its shadow get grouped into the same HighVariable, then without this
    /// Action the decompiler output shows duplicate COPY statements. This Action
    /// alters the defining op of the shadow so that the duplicate statement doesn't print.
    internal class ActionHideShadow : Action
    {
        public ActionHideShadow(string g)
            : base(ruleflags.rule_onceperfunc,"hideshadow", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionHideShadow(getGroup());
        }

        public override int apply(Funcdata data)
        {
            IEnumerator<Varnode> enditer = data.endDef(Varnode.varnode_flags.written);
            IEnumerator<Varnode> iter = data.beginDef();
            while (iter.MoveNext()) {
                HighVariable high = iter.Current.getHigh();
                if (high.isMark()) continue;
                if (data.getMerge().hideShadows(high))
                    count += 1;
                high.setMark();
            }
            iter = data.beginDef();
            while (iter.MoveNext()) {
                HighVariable high = iter.Current.getHigh();
                high.clearMark();
            }
            return 0;
        }
    }
}
