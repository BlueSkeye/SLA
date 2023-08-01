using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Assign initial high-level HighVariable objects to each Varnode
    internal class ActionAssignHigh : Action
    {
        public ActionAssignHigh(string g)
            : base(ruleflags.rule_onceperfunc,"assignhigh", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionAssignHigh(getGroup());
        }
        
        public override int apply(Funcdata data)
        {
            data.setHighLevel();
            return 0;
        }
    }
}
