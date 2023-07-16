using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Try to merge Varnodes specified by Symbols with multiple SymbolEntrys
    internal class ActionMergeMultiEntry : Action
    {
        public ActionMergeMultiEntry(string g)
            : base(rule_onceperfunc, "mergemultientry", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMergeMultiEntry(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().mergeMultiEntry();
            return 0;
        }
    }
}
