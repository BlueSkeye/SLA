using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Make \e required Varnode merges as dictated by CPUI_MULTIEQUAL, CPUI_INDIRECT, and \e addrtied property
    internal class ActionMergeRequired : Action
    {
        public ActionMergeRequired(string g)
            : base(rule_onceperfunc, "mergerequired", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeRequired(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeAddrTied();
            data.getMerge().groupPartials();
            data.getMerge().mergeMarker();
            return 0;
        }
    }
}
