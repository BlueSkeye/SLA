
namespace Sla.DECCORE
{
    /// \brief Action which checks if restart (sub)actions have been generated and restarts itself.
    /// Actions or Rules can request a restart on a Funcdata object by calling
    /// setRestartPending(true) on it. This action checks for the request then
    /// resets and reruns the group of Actions as appropriate.
    internal class ActionRestartGroup : ActionGroup
    {
        private int maxrestarts;           ///< Maximum number of restarts allowed
        private int curstart;          ///< Current restart iteration

        /// Construct this providing maximum number of restarts
        public ActionRestartGroup(ruleflags f, string nm, int max)
            :base(f, nm)
        {
            maxrestarts = max;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            ActionGroup? res = null;
            foreach (Action iter in list) {
                Action? ac = iter.clone(grouplist);
                if (null != ac) {
                    if (null == res) {
                        res = new ActionRestartGroup(flags, getName(), maxrestarts);
                    }
                    res.addAction(ac);
                }
            }
            return res;
        }

        public override void reset(Funcdata data)
        {
            curstart = 0;
            base.reset(data);
        }

        public override int apply(Funcdata data)
        {
            int res;

            if (curstart == -1) {
                // Already completed
                return 0;
            }
            while(true) {
                res = base.apply(data);
                if (res != 0) {
                    return res;
                }
                if (!data.hasRestartPending()) {
                    curstart = -1;
                    return 0;
                }
                if (data.isJumptableRecoveryOn()) {
                    // Don't restart within jumptable recovery
                    return 0;
                }
                curstart += 1;
                if (curstart > maxrestarts) {
                    data.warningHeader("Exceeded maximum restarts with more pending");
                    curstart = -1;
                    return 0;
                }
                data.getArch().clearAnalysis(data);

                // Reset everything but ourselves
                foreach (Action iter in list) {
                    // Reset each subrule
                    iter.reset(data);
                }
                status = statusflags.status_start;
            }
        }
    }
}
