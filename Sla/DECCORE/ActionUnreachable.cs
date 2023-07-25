using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Remove unreachable blocks
    internal class ActionUnreachable : Action
    {
        public ActionUnreachable(string g)
            : base(0,"unreachable", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionUnreachable(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            // Detect unreachable blocks and remove
            if (data.removeUnreachableBlocks(true, false)) {
                // Deleting at least one block
                count += 1;
            }
            return 0;
        }
    }
}
