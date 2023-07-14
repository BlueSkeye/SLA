using ghidra;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ghidra.XmlScan;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Large scale transformations applied to the varnode/op graph
    /// The base for objects that make changes to the syntax tree of a Funcdata
    /// The action is invoked through the apply(Funcdata &data) method.
    /// This base class keeps track of basic statistics about how the action is
    /// being applied.  Derived classes indicate that a change has been applied
    /// by incrementing the \b count field.
    /// With OPACTION_DEBUG macro defined, actions support a break point debugging in console mode.
    internal abstract class Action
    {
        /// Boolean behavior properties governing this particular Action
        public enum ruleflags
        {
            rule_repeatapply = 4,   ///< Apply rule repeatedly until no change
            rule_onceperfunc = 8,   ///< Apply rule once per function
            rule_oneactperfunc = 16,    ///< Makes a change only once per function
            rule_debug = 32,        ///< Print debug messages specifically for this action
            rule_warnings_on = 64,  ///< If this action makes a change, issue a warning
            rule_warnings_given = 128   ///< A warning has been issued for this action
        };

        /// Boolean properties describing the \e status of an action
        public enum statusflags
        {
            status_start = 1,       ///< At start of action
            status_breakstarthit = 2,   ///< At start after breakpoint
            status_repeat = 4,      ///< Repeating the same action
            status_mid = 8,     ///< In middle of action (use subclass status)
            status_end = 16,        ///< getFuncdata has completed once (for onceperfunc)
            status_actionbreak = 32 ///< Completed full action last time but indicated action break
        };
        
        /// Break points associated with an Action
        public enum breakflags
        {
            break_start = 1,        ///< Break at beginning of action
            tmpbreak_start = 2,     ///< Temporary break at start of action
            break_action = 4,       ///< Break if a change has been made
            tmpbreak_action = 8
        };

        /// Changes not including last call to apply()
        protected int lcount;
        /// Number of changes made by this action so far
        protected int count;
        /// Current status
        protected statusflags status;
        /// Breakpoint properties
        protected breakflags breakpoint;
        /// Behavior properties
        protected ruleflags flags;
        /// Number of times apply() has been called
        protected uint count_tests;
        /// Number of times apply() made changes
        protected uint count_apply;
        /// Name of the action
        protected string name;
        /// Base group this action belongs to
        protected string basegroup;

        /// Warn that this Action has applied
        /// If enabled, issue a warning that this Action has been applied
        /// \param glb is the controlling Architecture
        protected void issueWarning(Architecture glb)
        {
            if ((flags & (ruleflags.rule_warnings_on | ruleflags.rule_warnings_given)) == ruleflags.rule_warnings_on)
            {
                flags |= ruleflags.rule_warnings_given;
                glb.printMessage("WARNING: Applied action " + name);
            }
        }

        ///< Check start breakpoint
        /// Check if there was an active \e start break point on this action
        /// \return true if there was a start breakpoint
        protected bool checkStartBreak()
        {
            if ((breakpoint & (breakflags.break_start | breakflags.tmpbreak_start)) != 0) {
                // Clear breakpoint if temporary
                breakpoint &= ~(breakflags.tmpbreak_start);
                // Breakpoint was active
                return true;
            }
            // Breakpoint was not active
            return false;
        }

        /// Check action breakpoint
        /// Check if there was an active \e action breakpoint on this Action
        /// \return true if there was an action breakpoint
        protected bool checkActionBreak()
        {
            if ((breakpoint & (breakflags.break_action | breakflags.tmpbreak_action)) != 0) {
                // Clear temporary breakpoint
                breakpoint &= ~(breakflags.tmpbreak_action);
                // Breakpoint was active
                return true;
            }
            // Breakpoint was not active
            return false;
        }

        /// Enable warnings for this Action
        protected void turnOnWarnings()
        {
            flags |= ruleflags.rule_warnings_on;
        }

        /// Disable warnings for this Action
        protected void turnOffWarnings()
        {
            flags &= ~ruleflags.rule_warnings_on;
        }

        /// Base constructor for an Action
        /// Specify the name, group, and properties of the Action
        /// \param f is the collection of property flags
        /// \param nm is the Action name
        /// \param g is the Action group
        public Action(ruleflags f, string nm, string g)
        {
            flags = f;
            status = statusflags.status_start;
            breakpoint = 0;
            name = nm;
            basegroup = g;
            count_tests = 0;
            count_apply = 0;
        }

        /// Destructor
        ~Action()
        {
        }

#if OPACTION_DEBUG
        /// Turn on debugging
        /// If this Action matches the given name, enable debugging.
        /// \param nm is the Action name to match
        /// \return true if debugging was enabled
        public virtual bool turnOnDebug(string nm)
        {
            if (nm == name) {
                flags |= ruleflags.rule_debug;
                return true;
            }
            return false;
        }

        /// Turn off debugging
        /// If this Action matches the given name, disable debugging.
        /// \param nm is the Action name to match
        /// \return true if debugging was disabled
        public virtual bool turnOffDebug(string nm)
        {
            if (nm == name) {
                flags &= ~ruleflags.rule_debug;
                return true;
            }
            return false;
        }
#endif

        /// Dump statistics to stream
        /// Print out the collected statistics for the Action to stream
        /// \param s is the output stream
        public virtual void printStatistics(TextWriter s)
        {
            s.WriteLine($"{name} Tested={count_tests} Applied={count_apply}");
        }

        /// Perform this action (if necessary)
        /// Run \b this Action until completion or a breakpoint occurs. Depending
        /// on the behavior properties of this instance, the apply() method may get
        /// called many times or none.  Generally the number of changes made by
        /// the action is returned, but if a breakpoint occurs -1 is returned.
        /// A successive call to perform() will "continue" from the break point.
        /// \param data is the function being acted on
        /// \return the number of changes or -1
        public int perform(Funcdata data)
        {
            int res;

            do {
                switch (status) {
                    case statusflags.status_start:
                        // No changes made yet by this action
                        count = 0;
                        if (checkStartBreak()) {
                            status = statusflags.status_breakstarthit;
                            // Indicate partial completion
                            return -1;
                        }
                        count_tests += 1;
                        goto case statusflags.status_breakstarthit;
                    case statusflags.status_breakstarthit:
                    case statusflags.status_repeat:
                        lcount = count;
                        goto case statusflags.status_mid;
                    case statusflags.status_mid:
#if OPACTION_DEBUG
                        data.debugActivate();
#endif
                        res = apply(data);  // Start or continue action
#if OPACTION_DEBUG
                        data.debugModPrint(getName());
#endif
                        if (res < 0) {
                            // negative indicates partial completion
                            status = statusflags.status_mid;
                            return res;
                        }
                        else if (lcount < count) {
                            // Action has been applied
                            issueWarning(data.getArch());
                            count_apply += 1;
                            if (checkActionBreak()) {
                                status = statusflags.status_actionbreak;
                                // Indicate action breakpoint
                                return -1;
                            }
#if OPACTION_DEBUG
                            else if (data.debugBreak()) {
                                status = statusflags.status_actionbreak;
                                data.debugHandleBreak();
                                return -1;
                            }
#endif
                        }
                        break;
                    case statusflags.status_end:
                        // Rule applied, do not repeat until reset
                        return 0;
                    case statusflags.status_actionbreak:
                        // Returned -1 last time, but we do not reapply
                        // we either repeat, or return our count
                        break;
                }
                status = statusflags.status_repeat;
            } while ((lcount < count) && ((flags & ruleflags.rule_repeatapply) != 0));

            if ((flags & (ruleflags.rule_onceperfunc | ruleflags.rule_oneactperfunc)) != 0) {
                status = ((count > 0) || ((flags & ruleflags.rule_onceperfunc) != 0))
                    ? statusflags.status_end
                    : statusflags.status_start;
            }
            else {
                status = statusflags.status_start;
            }
            return count;
        }

        /// Set a breakpoint on this action
        /// A breakpoint can be placed on \b this Action or some sub-action by properly
        /// specifying the (sub)action name.
        /// \param tp is the type of breakpoint (\e break_start, break_action, etc.)
        /// \param specify is the (possibly sub)action to apply the break point to
        /// \return true if a breakpoint was successfully set
        public bool setBreakPoint(breakflags tp, string specify)
        {
            Action? res = getSubAction(specify);
            if (null != res) {
                res.breakpoint |= tp;
                return true;
            }
            ghidra.Rule? rule = getSubRule(specify);
            if (null != rule) {
                rule.setBreak(tp);
                return true;
            }
            return false;
        }

        /// Clear all breakpoints set on \b this Action
        public virtual void clearBreakPoints()
        {
            breakpoint = 0;
        }

        /// Set a warning on this action
        /// If enabled, a warning will be printed whenever this action applies.
        /// The warning can be toggled for \b this Action or some sub-action by
        /// specifying its name.
        /// \param val is the toggle value for the warning
        /// \param specify is the name of the action or sub-action to toggle
        /// \return true if the warning was successfully toggled
        public bool setWarning(bool val, string specify)
        {
            Action? res = getSubAction(specify);
            if (null != res) {
                if (val) {
                    res.turnOnWarnings();
                }
                else {
                    res.turnOffWarnings();
                }
                return true;
            }
            Rule? rule = getSubRule(specify);
            if (null != rule) {
                if (val) {
                    rule.turnOnWarnings();
                }
                else {
                    rule.turnOffWarnings();
                }
                return true;
            }
            return false;
        }

        /// Disable a specific Rule within \b this
        /// An individual Rule can be disabled by name, within \b this Action. It must
        /// be specified by a ':' separated name \e path, from the root Action down
        /// to the specific Rule.
        /// \param specify is the name path
        /// \return \b true if the Rule is successfully disabled
        public bool disableRule(string specify)
        {
            ghidra.Rule? rule = getSubRule(specify);
            if (null != rule) {
                rule.setDisable();
                return true;
            }
            return false;
        }

        /// Enable a specific Rule within \b this
        /// An individual Rule can be enabled by name, within \b this Action. It must
        /// be specified by a ':' separated name \e path, from the root Action down
        /// to the specific Rule.
        /// \param specify is the name path
        /// \return \b true if the Rule is successfully enabled
        public bool enableRule(string specify)
        {
            ghidra.Rule? rule = getSubRule(specify);
            if (null != rule) {
                rule.clearDisable();
                return true;
            }
            return false;
        }

        /// Get the Action's name
        public string getName() => name;

        /// Get the Action's group
        public string getGroup()=> basegroup;

        /// Get the current status of \b this Action
        public statusflags getStatus() => status;

        /// Get the number of times apply() was invoked
        public uint getNumTests() => count_tests;

        /// Get the number of times apply() made changes
        public uint getNumApply() => count_apply;

        /// \brief Clone the Action
        /// If \b this Action is a member of one of the groups in the grouplist,
        /// this returns a clone of the Action, otherwise NULL is returned.
        /// \param grouplist is the list of groups being cloned
        /// \return the cloned Action or NULL
        public abstract Action? clone(ActionGroupList grouplist);

        /// Reset the Action for a new function
        /// \param data is the new function \b this Action may affect
        public virtual void reset(Funcdata data)
        {
            status = statusflags.status_start;
            // Indicate a warning has not been given yet
            flags &= ~ruleflags.rule_warnings_given;
        }

        /// Reset the statistics
        /// Reset all the counts to zero
        public virtual void resetStats()
        {
            count_tests = 0;
            count_apply = 0;
        }

        /// \brief Make a single attempt to apply \b this Action
        /// This is the main entry point for applying changes to a function that
        /// are specific to \b this Action. The method can inspect whatever it wants
        /// to decide if the Action does or does not apply. Changes
        /// are indicated by incrementing the \b count field.
        /// \param data is the function to inspect/modify
        /// \return 0 for a complete application, -1 for a partial completion (due to breakpoint)
        public abstract int apply(Funcdata data);

        /// Print a description of this Action to stream
        /// The description is suitable for a console mode listing of actions
        /// \param s is the output stream
        /// \param num is a starting index to associate with the action (and its sub-actions)
        /// \param depth is amount of indent necessary before printing
        /// \return the next available index
        public virtual int print(TextWriter s, int num, int depth)
        {
            s.Write("{0:D4}{1}{2}{3}{4}",
                num, ((flags & ruleflags.rule_repeatapply) != 0) ? " repeat " : "        ",
                ((flags & ruleflags.rule_onceperfunc) != 0) ? '!' : ' ',
                ((breakpoint & (breakflags.break_start | breakflags.tmpbreak_start)) != 0) ? 'S' : ' ',
                ((breakpoint & (breakflags.break_action | breakflags.tmpbreak_action)) != 0) ? 'A' : ' ');
            for (int i = 0; i < depth * 5 + 2; ++i) {
                s.Write(' ');
            }
            s.Write(name);
            return num + 1;
        }

        /// Print status to stream
        /// This will the Action name and the next step to execute
        /// \param s is the output stream
        public virtual void printState(TextWriter s)
        {
            s.Write(name);
            switch (status) {
                case statusflags.status_repeat:
                case statusflags.status_breakstarthit:
                case statusflags.status_start:
                    s.Write(" start");
                    break;
                case statusflags.status_mid:
                    s.Write(':');
                    break;
                case statusflags.status_end:
                    s.Write(" end");
                    break;
            }
        }

        /// Retrieve a specific sub-action by name
        /// If this Action matches the given name, it is returned. If the
        /// name matches a sub-action, this is returned.
        /// \param specify is the action name to match
        /// \return the matching Action or sub-action
        public virtual Action? getSubAction(string specify)
        {
            return (name == specify) ? this : null;
        }

        /// Retrieve a specific sub-rule by name
        /// Find a Rule, as a component of \b this Action, with the given name.
        /// \param specify is the name of the rule
        /// \return the matching sub-rule
        public virtual Rule? getSubRule(string specify)
        {
            return null;
        }

        /// Pull the next token from a ':' separated list of Action and Rule names
        /// \param token will be filled with string up to the next ':'
        /// \param remain will be whats left of the list of removing the token and ':'
        /// \param is the list to pull the token from
        internal static void next_specifyterm(out string token, out string remain,
            string specify)
        {
            int res = specify.IndexOf(':');
            if (-1 != res) {
                token = specify.Substring(0, res);
                remain = specify.Substring(res + 1);
            }
            else {
                token = specify;
                remain = string.Empty;
            }
        }
    }
}
