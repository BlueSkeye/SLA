
using VarnodeDefSet = System.Collections.Generic.HashSet<Sla.DECCORE.Varnode>; // VarnodeDefSet : A set of Varnodes sorted by definition (then location)

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
            : base(rule_onceperfunc,"hideshadow", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionHideShadow(getGroup());
        }

        public override int apply(Funcdata data)
        {
            VarnodeDefSet::const_iterator iter, enditer;
            HighVariable* high;

            enditer = data.endDef(Varnode.varnode_flags.written);
            for (iter = data.beginDef(); iter != enditer; ++iter)
            {
                high = (*iter).getHigh();
                if (high.isMark()) continue;
                if (data.getMerge().hideShadows(high))
                    count += 1;
                high.setMark();
            }
            for (iter = data.beginDef(); iter != enditer; ++iter)
            {
                high = (*iter).getHigh();
                high.clearMark();
            }
            return 0;
        }
    }
}
