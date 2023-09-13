using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A group of actions (generally) applied in sequence
    /// This is a a list of Action objects, which are usually applied in sequence.
    /// But the behavior properties of each individual Action may affect this.
    /// Properties (like rule_repeatapply) may be put directly to this group
    /// that also affect how the Actions are applied.
    internal class ActionGroup : Action
    {
        /// List of actions to perform in the group
        protected List<Action> list;
        /// Current action being applied
        protected IEnumerator<Action> state;
        // ADDED
        private bool stateCompleted = true;

        /// Construct given properties and a name
        public ActionGroup(ruleflags f, string nm)
            : base(f, nm, "")
        {
        }

        /// Destructor
        ~ActionGroup()
        {
            foreach (Action action in list) {
                // delete action;
            }
        }

        /// Add an Action to the group
        /// To be used only during the construction of \b this ActionGroup. This routine
        /// adds an Action to the end of this group's list.
        /// \param ac is the Action to add
        public void addAction(Action ac)
        {
            list.Add(ac);
        }

        public override void clearBreakPoints()
        {
            foreach (Action action in list) {
                action.clearBreakPoints();
            }
            base.clearBreakPoints();
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            ActionGroup? res = null;
            foreach (Action action in list) {
                Action? ac = action.clone(grouplist);
                if (null != ac) {
                    if (null == res) {
                        res = new ActionGroup(flags, getName());
                    }
                    res.addAction(ac);
                }
            }
            return res;
        }

        public override void reset(Funcdata data)
        {
            base.reset(data);
            foreach (Action action in list) {
                // Reset each subrule
                action.reset(data);
            }
        }

        public override void resetStats()
        {
            base.resetStats();
            foreach (Action action in list) {
                // Reset each subrule
                action.resetStats();
            }
        }

        public override int apply(Funcdata data)
        {
            int res;

            if (status != statusflags.status_mid) {
                // Initialize the derived action
                state = list.GetEnumerator();
                stateCompleted = !state.MoveNext();
            }
            for (; !stateCompleted; stateCompleted = !state.MoveNext()) {
                res = state.Current.perform(data);
                if (res > 0) {       // A change was made
                    count += res;
                    if (checkActionBreak()) {
                        // Check if this is an action breakpoint
                        stateCompleted = !state.MoveNext();
                        return -1;
                    }
                }
                else if (res < 0){
                    // Partial completion of member
                    // equates to partial completion of group action
                    return -1;
                }
            }
            // Indicate successful completion
            return 0;
        }

        public override int print(TextWriter s, int num, int depth)
        {
            num = base.print(s, num, depth);
            s.WriteLine();
            foreach (Action action in list) {
                num = action.print(s, num, depth + 1);
                if ((null != state) && (state.Current == action)) {
                    s.Write("  <-- ");
                }
                s.WriteLine();
            }
            return num;
        }

        public override void printState(TextWriter s)
        {
            Action subact;

            base.printState(s);
            if (status == statusflags.status_mid) {
                subact = state.Current;
                subact.printState(s);
            }
        }

        public override Action? getSubAction(string specify)
        {
            string token;
            string remain;
            next_specifyterm(out token, out remain, specify);
            if (name == token) {
                if (0 == remain.Length) {
                    return this;
                }
            }
            else {
                // Still have to match entire specify
                remain = specify;
            }

            // List<Action*>::iterator iter;
            Action? lastaction = null;
            int matchcount = 0;
            foreach (Action iter in list) {
                Action? testaction = iter.getSubAction(remain);
                if (null != testaction) {
                    lastaction = testaction;
                    matchcount += 1;
                    if (matchcount > 1) {
                        return null;
                    }
                }
            }
            return lastaction;
        }

        public override Rule? getSubRule(string specify)
        {
            string token;
            string remain;
            next_specifyterm(out token, out remain, specify);
            if (name == token) {
                if (0 == remain.Length) {
                    return null;
                }
            }
            else {
                // Still have to match entire specify
                remain = specify;
            }

            // List<Action*>::iterator iter;
            Rule? lastrule = null;
            int matchcount = 0;
            foreach (Action iter in list) {
                Rule? testrule = iter.getSubRule(remain);
                if (null != testrule) {
                    lastrule = testrule;
                    matchcount += 1;
                    if (matchcount > 1) {
                        return null;
                    }
                }
            }
            return lastrule;
        }

#if OPACTION_DEBUG
        public virtual bool turnOnDebug(string nm)
        {
            if (Action::turnOnDebug(nm)) {
                return true;
            }
            foreach(Action iter in list) {
                if (iter.turnOnDebug(nm)) {
                    return true;
                }
            }
            return false;
        }

        public virtual bool turnOffDebug(string nm)
        {
            if (Action::turnOffDebug(nm)) {
                return true;
            }
            foreach(Action iter in list) {
                if (iter.turnOffDebug(nm)) {
                    return true;
                }
            }
            return false;
        }
#endif
        public override void printStatistics(TextWriter s)
        {
            base.printStatistics(s);
            foreach (Action iter in list) {
                iter.printStatistics(s);
            }
        }
    }
}
