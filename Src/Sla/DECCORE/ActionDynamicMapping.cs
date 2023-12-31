﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Attach \e dynamically mapped symbols to Varnodes in time for data-type propagation
    internal class ActionDynamicMapping : Action
    {
        public ActionDynamicMapping(string g)
            : base(0,"dynamicmapping", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDynamicMapping(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ScopeLocal localmap = data.getScopeLocal();
            IEnumerator<SymbolEntry> iter = localmap.beginDynamic();
            // enditer = localmap.endDynamic();
            DynamicHash dhash = new DynamicHash();
            while (iter.MoveNext()) {
                SymbolEntry entry = iter.Current;
                if (data.attemptDynamicMapping(entry, dhash))
                    count += 1;
            }
            return 0;
        }
    }
}
