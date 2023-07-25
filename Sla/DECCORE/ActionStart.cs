using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Gather raw p-code for a function.
    internal class ActionStart : Action
    {
        ///< Constructor
        public ActionStart(string g)
            : base(0,"start", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionStart(getGroup());
        }

        public override virtual  apply(Funcdata data)
        {
            data.startProcessing();
            return 0;
        }
    }
}
