using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionToggleRule : ArchOption
    {
        public OptionToggleRule()
        {
            name = "togglerule";
        }

        /// \class OptionToggleRule
        /// \brief Toggle whether a specific Rule is applied in the current Action
        ///
        /// The first parameter must be a name \e path describing the unique Rule instance
        /// to be toggled.  The second parameter is "on" to \e enable the Rule, "off" to \e disable.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if (p1.size() == 0)
                throw ParseError("Must specify rule path");
            if (p2.size() == 0)
                throw ParseError("Must specify on/off");
            bool val = onOrOff(p2);

            Action* root = glb.allacts.getCurrent();
            if (root == (Action*)0)
                throw new LowlevelError("Missing current action");
            string res;
            if (!val)
            {
                if (root.disableRule(p1))
                    res = "Successfully disabled";
                else
                    res = "Failed to disable";
                res += " rule";
            }
            else
            {
                if (root.enableRule(p1))
                    res = "Successfully enabled";
                else
                    res = "Failed to enable";
                res += " rule";
            }
            return res;
        }
    }
}
