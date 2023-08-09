using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Mark illegal Varnode inputs used only in OpCode.CPUI_INDIRECT ops
    internal class ActionMarkIndirectOnly : Action
    {
        public ActionMarkIndirectOnly(string g)
            : base(ruleflags.rule_onceperfunc, "markindirectonly", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionMarkIndirectOnly(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.markIndirectOnly(); return 0;
        }
    }
}
