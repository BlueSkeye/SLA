using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Apply any overridden forced gotos
    internal class ActionForceGoto : Action
    {
        /// Constructor
        public ActionForceGoto(string g)
            : base(0,"forcegoto", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionForceGoto(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            data.getOverride().applyForceGoto(data);
            return 0;
        }
    }
}
