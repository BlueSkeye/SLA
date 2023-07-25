using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Allow type recovery to start happening
    /// The presence of \b this Action causes the function to be marked that data-type analysis
    /// will be performed.  Then when \b this action is applied during analysis, the function is marked
    /// that data-type analysis has started.
    internal class ActionStartTypes : Action
    {
        /// Constructor
        public ActionStartTypes(string g)
            : base(0, "starttypes", g)
        {
        }

        public override void reset(Funcdata data)
        {
            data.setTypeRecovery(true);
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionStartTypes(getGroup());
        }

        public override int apply(Funcdata data)
        {
            if (data.startTypeRecovery()) count += 1;
            return 0;
        }
    }
}
