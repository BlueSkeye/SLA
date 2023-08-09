using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Make final attachments of \e dynamically mapped symbols to Varnodes
    internal class ActionDynamicSymbols : Action
    {
        public ActionDynamicSymbols(string g)
            : base(rule_onceperfunc,"dynamicsymbols", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDynamicSymbols(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ScopeLocal localmap = data.getScopeLocal();
            IEnumerator<SymbolEntry> iter, enditer;
            iter = localmap.beginDynamic();
            enditer = localmap.endDynamic();
            DynamicHash dhash;
            while (iter != enditer) {
                SymbolEntry entry = &(*iter);
                ++iter;
                if (data.attemptDynamicMappingLate(entry, dhash))
                    count += 1;
            }
            return 0;
        }
    }
}
