using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief A pool of Rules that apply simultaneously
    ///
    /// This class groups together a set of Rules as a formal Action.
    /// Rules are given an opportunity to apply to every PcodeOp in a function.
    /// Usually rule_repeatapply is enabled for this action, which causes
    /// all Rules to apply repeatedly until no Rule can make an additional change.
    internal class ActionPool : Action
    {
        /// The set of Rules in this ActionPool
        private List<Rule> allrules;
        /// Rules associated with each OpCode
        private List<Rule>[] perop = new List<Rule>[(int)OpCode.CPUI_MAX];
        /// Current PcodeOp up for rule application
        private PcodeOpTree::const_iterator op_state;
        /// Iterator over Rules for one OpCode
        private int rule_index;

        /// Apply the next possible Rule to a PcodeOp
        /// This method attempts to apply each Rule to the current PcodeOp
        /// Action breakpoints are checked if the Rule successfully applies.
        /// 0 is returned for no breakpoint, -1 if a breakpoint occurs.
        /// If a breakpoint did occur, an additional call to processOp() will
        /// pick up where it left off before the breakpoint. The PcodeOp iterator is advanced.
        /// \param op is the current PcodeOp
        /// \param data is the function being transformed
        /// \return 0 if no breakpoint, -1 otherwise
        private int processOp(PcodeOp op, Funcdata data)
        {
            if (op.isDead()) {
                op_state++;
                data.opDeadAndGone(op);
                rule_index = 0;
                return 0;
            }
            uint opc = op.code();
            while (rule_index < perop[opc].Count) {
                Rule rl = perop[opc][rule_index++];
                if (rl.isDisabled()) {
                    continue;
                }
#if OPACTION_DEBUG
                data.debugActivate();
#endif
                rl.count_tests += 1;
                int res = rl.applyOp(op, data);
#if OPACTION_DEBUG
                data.debugModPrint(rl->getName());
#endif
                if (res > 0) {
                    rl.count_apply += 1;
                    count += res;
                    // Check if we need to issue a warning
                    rl.issueWarning(data.getArch());
                    if (rl.checkActionBreak()) {
                        return -1;
                    }
#if OPACTION_DEBUG
                    if (data.debugBreak()) {
                        data.debugHandleBreak();
                        return -1;
                    }
#endif
                    if (op.isDead()) {
                        break;
                    }
                    if (opc != op.code()) {
                        // Set of rules to apply to this op has changed
                        opc = op.code();
                        rule_index = 0;
                    }
                }
                else if (opc != op.code()) {
                    data.getArch().printMessage(
                        $"ERROR: Rule {rl.getName()} changed op without returning result of 1!");
                    opc = op.code();
                    rule_index = 0;
                }
            }
            op_state++;
            rule_index = 0;
            return 0;
        }

        /// Construct providing properties and name
        public ActionPool(ruleflags f, string nm)
            : base(f, nm,"")
        {
        }

        /// Destructor
        ~ActionPool()
        {
            foreach (Rule iter in allrules) {
                delete iter;
            }
        }

        /// Add a Rule to the pool
        /// This method should only be invoked during construction of this ActionPool
        /// A single Rule is added to the pool. The Rule's OpCode is inspected by this method.
        /// \param rl is the Rule to add
        public void addRule(Rule rl)
        {
            List<OpCode> oplist = new List<OpCode>();

            allrules.Add(rl);
            rl.getOpList(oplist);
            foreach (OpCode iter in oplist) {
                // Add rule to list for each op it registers for
                perop[(int)iter].Add(rl);
            }
        }

        public override void clearBreakPoints()
        {
            foreach (Rule iter in allrules) {
                iter.clearBreakPoints();
            }
            base.clearBreakPoints();
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            ActionPool? res = null;
            foreach (Rule iter in allrules) {
                Rule rl = iter.clone(grouplist);
                if (null != rl) {
                    if (null == res) {
                        res = new ActionPool(flags, getName());
                    }
                    res.addRule(rl);
                }
            }
            return res;
        }

        public override void reset(Funcdata data)
        {
            base.reset(data);
            foreach (Rule iter in allrules) {
                iter.reset(data);
            }
        }

        public override void resetStats()
        {
            base.resetStats();
            foreach (Rule iter in allrules) {
                iter.resetStats();
            }
        }

        public override int apply(Funcdata data)
        {
            if (status != statusflags.status_mid) {
                // Initialize the derived action
                op_state = data.beginOpAll();
                rule_index = 0;
            }
            for (; op_state != data.endOpAll();) {
                if (0 != processOp((*op_state).second, data)) {
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
            depth += 1;
            foreach (Rule iter in allrules) {
                Rule rl = iter;
                s.Write("{num:D4}");
                s.Write(rl.isDisabled() ? 'D' : ' ');
                s.Write(((rl.getBreakPoint() & (breakflags.break_action | breakflags.tmpbreak_action)) != 0) ? 'A' : ' ');
                for (int i = 0; i < depth * 5 + 2; ++i) {
                    s.Write(' ');
                }
                s.WriteLine(rl.getName());
                num += 1;
            }
            return num;
        }

        public override void printState(TextWriter s)
        {
            base.printState(s);
            if (status == statusflags.status_mid) {
                PcodeOp op = (*op_state).second;
                s.Write($" {op.getSeqNum()}");
            }
        }

        public override Rule? getSubRule(string specify)
        {
            string token;
            string remain;
            next_specifyterm(out token, out remain, specify);
            if (name == token) {
                if (0 == remain.Length) {
                    // Match, but not a rule
                    return null;
                }
            }
            else {
                // Still have to match entire specify
                remain = specify;
            }

            Rule? lastrule = null;
            int matchcount = 0;
            foreach (Rule iter in allrules) {
                Rule testrule = iter;
                if (testrule.getName() == remain) {
                    lastrule = testrule;
                    matchcount += 1;
                    if (matchcount > 1) {
                        return null;
                    }
                }
            }
            return lastrule;
        }

        public override void printStatistics(TextWriter s)
        {
            base.printStatistics(s);
            foreach (Rule iter in allrules) {
                iter.printStatistics(s);
            }
        }

#if OPACTION_DEBUG
        public override bool turnOnDebug(string nm)
        {
            if (base.turnOnDebug(nm)) {
                return true;
            }
            foreach (Rule iter in allrules) {
                if (iter.turnOnDebug(nm)) {
                    return true;
                }
            }
            return false;
        }

        public override bool turnOffDebug(string nm)
        {
            vector<Rule *>::iterator iter;

            if (base.turnOffDebug(nm)) {
                return true;
            }
            foreach (Rule iter in allrules) {
                if ((*iter)->turnOffDebug(nm)) {
                    return true;
                }
            }
            return false;
        }
#endif
    }
}
