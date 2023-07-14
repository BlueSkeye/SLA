using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Do any post-processing after decompilation
    internal class ActionStop : Action
    {
        /// Constructor
        public ActionStop(string g)
            : base(0, "stop", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionStop(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.stopProcessing(); return 0;
        }
    }
}
