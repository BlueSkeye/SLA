
namespace Sla.DECCORE
{
    /// \brief Mark COPY operations between Varnodes representing the object as \e non-printing
    internal class ActionCopyMarker : Action
    {
        public ActionCopyMarker(string g)
            : base(ruleflags.rule_onceperfunc,"copymarker", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionCopyMarker(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().markInternalCopies();
            return 0;
        }
    }
}
