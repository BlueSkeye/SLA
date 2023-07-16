using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Mark COPY operations between Varnodes representing the object as \e non-printing
    internal class ActionCopyMarker : Action
    {
        public ActionCopyMarker(string g)
            : base(rule_onceperfunc,"copymarker", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionCopyMarker(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.getMerge().markInternalCopies();
            return 0;
        }
    }
}
