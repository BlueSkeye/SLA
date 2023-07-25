using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionWarning : ArchOption
    {
        public OptionWarning()
        {
            name = "warning";
        }

        /// \class OptionWarning
        /// \brief Toggle whether a warning should be issued if a specific action/rule is applied.
        ///
        /// The first parameter gives the name of the Action or RuleAction.  The second parameter
        /// is "on" to turn on warnings, "off" to turn them off.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if (p1.size() == 0)
                throw ParseError("No action/rule specified");
            bool val;
            if (p2.size() == 0)
                val = true;
            else
                val = onOrOff(p2);
            bool res = glb->allacts.getCurrent()->setWarning(val, p1);
            if (!res)
                throw RecovError("Bad action/rule specifier: " + p1);
            string prop;
            prop = val ? "on" : "off";
            return "Warnings for " + p1 + " turned " + prop;
        }
    }
}
