using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Mark Varnode objects that hold stack-pointer values and set-up special data-type
    internal class ActionSpacebase : Action
    {
        public ActionSpacebase(string g)
            : base(0, "spacebase", g)
        {
        }

        public override Action clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionSpacebase(getGroup());
        }

        public override int apply(Funcdata data)
        {
            data.spacebase();
            return 0;
        }
    }
}
