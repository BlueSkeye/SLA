using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Start clean up after main transform phase
    internal class ActionStartCleanUp : Action
    {
        /// Constructor
        public ActionStartCleanUp(string g)
            : base(0, "startcleanup", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionStartCleanUp(getGroup());
        }
     
        public override int apply(Funcdata data)
        {
            data.startCleanUp(); return 0;
        }
    }
}
