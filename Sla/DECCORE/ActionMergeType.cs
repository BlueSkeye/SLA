using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Try to merge Varnodes of the same type (if they don't hold different values at the same time)
    internal class ActionMergeType : Action
    {
        public ActionMergeType(string g)
            : base(rule_onceperfunc, "mergetype", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeType(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeByDatatype(data.beginLoc(), data.endLoc());
            return 0;
        }
    }
}
