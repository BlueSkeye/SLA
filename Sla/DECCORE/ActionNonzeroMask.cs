using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Calculate the non-zero mask property on all Varnode objects.
    internal class ActionNonzeroMask : Action
    {
        public ActionNonzeroMask(string g)
            : base(0, "nonzeromask", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionNonzeroMask(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.calcNZMask();
            return 0;
        }
    }
}
