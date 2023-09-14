
namespace Sla.DECCORE
{
    /// \brief Make final attachments of \e dynamically mapped symbols to Varnodes
    internal class ActionDynamicSymbols : Action
    {
        public ActionDynamicSymbols(string g)
            : base(ruleflags.rule_onceperfunc,"dynamicsymbols", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDynamicSymbols(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ScopeLocal localmap = data.getScopeLocal();
            IEnumerator<SymbolEntry> iter = localmap.beginDynamic();
            DynamicHash dhash = new DynamicHash();
            while (iter.MoveNext()) {
                SymbolEntry entry = iter.Current;
                if (data.attemptDynamicMappingLate(entry, dhash))
                    count += 1;
            }
            return 0;
        }
    }
}
