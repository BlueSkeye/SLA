using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Create symbols that map out the local stack-frame for the function.
    ///
    /// This produces the final set of symbols on the stack.
    internal class ActionRestructureHigh : Action
    {
        public ActionRestructureHigh(string g)
            : base(0,"restructure_high", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionRestructureHigh(getGroup());
        }

        public override int apply(Funcdata data)
        {
            if (!data.isHighOn()) return 0;
            ScopeLocal* l1 = data.getScopeLocal();

#if OPACTION_DEBUG
            if ((flags & rule_debug) != 0)
                l1->turnOnDebug();
#endif

            l1->restructureHigh();
            if (data.syncVarnodesWithSymbols(l1, true, true))
                count += 1;

#if OPACTION_DEBUG
            if ((flags & rule_debug) == 0) return 0;
            l1->turnOffDebug();
            ostringstream s;
            data.getScopeLocal()->printEntries(s);
            data.getArch()->printDebug(s.str());
#endif
            return 0;
        }
    }
}
