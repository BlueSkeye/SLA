using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionAllowContextSet : ArchOption
    {
        public OptionAllowContextSet()
        {
            name = "allowcontextset";
        }

        /// \class OptionAllowContextSet
        /// \brief Toggle whether the disassembly engine is allowed to modify context
        ///
        /// If the first parameter is "on", disassembly can make changes to context
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string prop = val ? "on" : "off";
            string res = "Toggled allowcontextset to " + prop;
            glb->translate->allowContextSet(val);

            return res;
        }
    }
}
