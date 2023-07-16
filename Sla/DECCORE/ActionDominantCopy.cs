using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Replace COPYs from the same source with a single dominant COPY
    internal class ActionDominantCopy : Action
    {
        public ActionDominantCopy(string g)
            : base(rule_onceperfunc,"dominantcopy", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDominantCopy(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().processCopyTrims();
            return 0;
        }
    }
}
