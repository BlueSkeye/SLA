using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Try to merge an op's input Varnode to its output, if they are at the same storage location.
    internal class ActionMergeAdjacent : Action
    {
        public ActionMergeAdjacent(string g)
            : base(rule_onceperfunc, "mergeadjacent", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeAdjacent(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeAdjacent();
            return 0;
        }
    }
}
