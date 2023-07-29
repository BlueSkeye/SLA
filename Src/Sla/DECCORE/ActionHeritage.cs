using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class ActionHeritage : Action
    {
        public ActionHeritage(string g)
            : base(0, "heritage", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionHeritage(getGroup());
        }
        
        public override int apply(Funcdata data)
        {
            data.opHeritage();
            return 0;
        }
    }
}
