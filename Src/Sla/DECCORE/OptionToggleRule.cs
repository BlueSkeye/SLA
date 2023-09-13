using Sla.CORE;
using Sla.EXTRA;

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
            if (p1.Length == 0)
                throw new ParseError("Must specify rule path");
            if (p2.Length == 0)
                throw new ParseError("Must specify on/off");
            bool val = onOrOff(p2);

            Action? root = glb.allacts.getCurrent();
            if (root == (Action)null)
                throw new LowlevelError("Missing current action");
            string res;
            if (!val) {
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
