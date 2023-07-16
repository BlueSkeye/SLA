using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Create symbols for any discovered global variables in the function.
    internal class ActionMapGlobals : Action
    {
        public ActionMapGlobals(string g)
            : base(rule_onceperfunc, "mapglobals", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMapGlobals(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.mapGlobals();
            return 0;
        }
    }
}
