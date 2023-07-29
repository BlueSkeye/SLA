using ghidra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Class for performing a single transformation on a PcodeOp or Varnode
    /// A Rule, through its applyOp() method, is handed a specific PcodeOp as a potential
    /// point to apply. It determines if it can apply at that point, then makes any changes.
    /// Rules inform the system of what types of PcodeOps they can possibly apply to through
    /// the getOpList() method. A set of Rules are pooled together into a single Action via
    /// the ActionPool, which efficiently applies each Rule across a whole function.
    /// A Rule supports the same breakpoint properties as an Action.
    /// A Rule is allowed to keep state that is specific to a given function (Funcdata).
    /// The reset() method is invoked to purge this state for each new function to be transformed.
    internal abstract class Rule
    {
        /// Properties associated with a Rule
        public enum typeflags
        {
            /// Is this rule disabled
            type_disable = 1,
            /// Print debug info specific for this rule
            rule_debug = 2,
            /// A warning is issued if this rule is applied
            warnings_on = 4,
            /// Set if a warning for this rule has been given before
            warnings_given = 8
        }

        // friend class ActionPool;
        /// Properties enabled with \b this Rule
        private typeflags flags;
        /// Breakpoint(s) enabled for \b this Rule
        private Action.breakflags breakpoint;
        /// Name of the Rule
        private string name;
        /// Group to which \b this Rule belongs
        private string basegroup;
        /// Number of times \b this Rule has attempted to apply
        private uint count_tests;
        /// Number of times \b this Rule has successfully been applied
        private uint count_apply;

        /// If enabled, print a warning that this Rule has been applied
        /// This method is called whenever \b this Rule applies. If warnings have been
        /// enabled for the Rule via turnOnWarnings(), this method will print a message
        /// indicating the Rule has been applied.  Even with repeat calls, the message
        /// will only be printed once (until reset() is called)
        /// \param glb is the Architecture holding the console to print to
        private void issueWarning(Architecture glb)
        {
            if ((flags & (typeflags.warnings_on | typeflags.warnings_given)) == typeflags.warnings_on) {
                flags |= typeflags.warnings_given;
                glb.printMessage("WARNING: Applied rule " + name);
            }
        }

        /// Construct given group, properties name
        /// \param g is the groupname to which \b this Rule belongs
        /// \param fl is the set of properties
        /// \param nm is the name of the Rule
        public Rule(string g, typeflags fl, string nm)
        {
            flags = fl;
            name = nm;
            breakpoint = 0;
            basegroup = g;
            count_tests = 0;
            count_apply = 0;
        }

    /// Destructor
        ~Rule()
        {
        }

        /// Return the name of \b this Rule
        public string getName() => name;

        /// Return the group \b this Rule belongs to
        public string getGroup() => basegroup;

        /// Get number of attempted applications
        public uint getNumTests() => count_tests;

        /// Get number of successful applications
        public uint getNumApply() => count_apply;

        /// Set a breakpoint on \b this Rule
        public void setBreak(Action.breakflags tp)
        {
            breakpoint |= tp;
        }

        /// Clear a breakpoint on \b this Rule
        public void clearBreak(Action.breakflags tp)
        {
            breakpoint &= ~tp;
        }

        /// Clear all breakpoints on \b this Rule
        public void clearBreakPoints()
        {
            breakpoint = 0;
        }

        /// Enable warnings for \b this Rule
        public void turnOnWarnings()
        {
            flags |= typeflags.warnings_on;
        }

        /// Disable warnings for \b this Rule
        public void turnOffWarnings()
        {
            flags &= ~typeflags.warnings_on;
        }

        /// Return \b true if \b this Rule is disabled
        public bool isDisabled() => ((flags & typeflags.type_disable)!= 0);

        /// Disable this Rule (within its pool)
        public void setDisable()
        {
            flags |= typeflags.type_disable;
        }

        /// Enable this Rule (within its pool)
        public void clearDisable()
        {
            flags &= ~typeflags.type_disable;
        }

        /// Check if an action breakpoint is turned on
        /// This method is called every time the Rule successfully applies. If it returns
        /// \b true, this indicates to the system that an action breakpoint has occurred.
        /// \return true if an action breakpoint should occur because of this Rule
        public bool checkActionBreak()
        {
            if ((breakpoint & (Action.breakflags.break_action | Action.breakflags.tmpbreak_action)) != 0)
            {
                // Clear temporary breakpoint
                breakpoint &= ~(Action.breakflags.tmpbreak_action);
                // Breakpoint was active
                return true;
            }
            // Breakpoint was not active
            return false;
        }

        /// Return breakpoint toggles
        public Action.breakflags getBreakPoint()
        {
            return breakpoint;
        }

        /// \brief Clone the Rule
        /// If \b this Rule is a member of one of the groups in the grouplist,
        /// this returns a clone of the Rule, otherwise NULL is returned.
        /// \param grouplist is the list of groups being cloned
        /// \return the cloned Rule or NULL
        public abstract Rule clone(ActionGroupList grouplist);

        /// List of op codes this rule operates on
        /// Populate the given array with all possible OpCodes this Rule might apply to.
        /// By default, this method returns all possible OpCodes
        /// \param oplist is the array to populate
        public virtual void getOpList(List<OpCode> oplist)
        {
            for (OpCode i = 0; i < OpCode.CPUI_MAX; ++i) {
                oplist.Add(i);
            }
        }

        /// \brief Attempt to apply \b this Rule
        /// This method contains the main logic for applying the Rule. It must use a given
        /// PcodeOp as the point at which the Rule applies. If it does apply,
        /// changes are made directly to the function and 1 (non-zero) is returned, otherwise 0
        /// is returned.
        /// \param op is the given PcodeOp where the Rule may apply
        /// \param data is the function to which to apply
        public virtual int applyOp(PcodeOp op, Funcdata data)
        {
            return 0;
        }

        /// Reset \b this Rule
        /// Any state that is specific to a particular function is cleared by this method.
        /// This method can be used to initialize a Rule based on a new function it will apply to
        /// \param data is the \e new function about to be transformed
        public virtual void reset(Funcdata data)
        {
            // Indicate that warning has not yet been given
            flags &= ~typeflags.warnings_given;
        }

        /// Reset Rule statistics
        /// Counts of when this Rule has been attempted/applied are reset to zero.
        /// Derived Rules may reset their own statistics.
        public virtual void resetStats()
        {
            count_tests = 0;
            count_apply = 0;
        }

        /// Print statistics for \b this Rule
        /// Print the accumulated counts associated with applying this Rule to stream.
        /// This method is intended for console mode debugging. Derived Rules may
        /// override this to display their own statistics.
        /// \param s is the output stream
        public virtual void printStatistics(TextWriter s)
        {
            s.WriteLine($"{name} Tested={count_tests} Applied={count_apply}");
        }

#if OPACTION_DEBUG
        /// Turn on debugging
        /// If \b this Rule has the given name, then enable debugging.
        /// \param nm is the given name to match
        /// \return true if debugging was enabled
        public virtual bool turnOnDebug(string nm)
        {
            if (nm == name) {
                flags |= typeflags.rule_debug;
                return true;
            }
            return false;
        }
        
        /// Turn off debugging
        /// If \b this Rule has the given name, then disable debugging.
        /// \param nm is the given name to match
        /// \return true if debugging was disabled
        public virtual bool turnOffDebug(string nm)
        {
            if (nm == name) {
                flags &= ~rule_debug;
                return true;
            }
            return false;
        }
#endif
    }
}
