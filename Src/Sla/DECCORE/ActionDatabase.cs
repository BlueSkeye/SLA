using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Database of root Action objects that can be used to transform a function
    /// This is a container for Action objects. It also manages \b root Action objects,
    /// which encapsulate a complete transformation system that can be applied to functions.
    /// \e Root Action objects are derived from a single \b universal Action object that
    /// has every possible sub-action within it.  A \e root Action has its own name and
    /// is derived from the \e universal via a grouplist, which lists a particular subset of
    /// Action and Rule groups to use for the root.  A new \e root Action is created by
    /// providing a new grouplist via setGroup() or modifying an existing grouplist.
    /// This class is intended to be instantiated as a singleton and keeps track of
    /// the \e current root Action, which is the one that will be actively applied to functions.
    internal class ActionDatabase
    {
        private static readonly string[] members = new string[] {
            "base", "protorecovery", "protorecovery_a", "deindirect", "localrecovery",
            "deadcode", "typerecovery", "stackptrflow",
            "blockrecovery", "stackvars", "deadcontrolflow", "switchnorm",
            "cleanup", "splitcopy", "splitpointer", "merge", "dynamic", "casts", "analysis",
            "fixateglobals", "fixateproto",
            "segment", "returnsplit", "nodejoin", "doubleload", "doubleprecis",
            "unreachable", "subvar", "floatprecision", "conditionalexe", ""
        };

        private static readonly string[] jumptab = new string[] {
            "base", "noproto", "localrecovery", "deadcode", "stackptrflow",
            "stackvars", "analysis", "segment", "subvar", "conditionalexe", ""
        };

        private static readonly string[] normali = new string[] {
            "base", "protorecovery", "protorecovery_b", "deindirect", "localrecovery",
            "deadcode", "stackptrflow", "normalanalysis",
            "stackvars", "deadcontrolflow", "analysis", "fixateproto", "nodejoin",
            "unreachable", "subvar", "floatprecision", "normalizebranches",
            "conditionalexe", ""
        };

        private static readonly string[] paramid = new string[] {
            "base", "protorecovery", "protorecovery_b", "deindirect", "localrecovery",
            "deadcode", "typerecovery", "stackptrflow", "siganalysis",
            "stackvars", "deadcontrolflow", "analysis", "fixateproto",
            "unreachable", "subvar", "floatprecision", "conditionalexe", ""
        };

        private static readonly string[] regmemb = new string[] {
            "base", "analysis", "subvar", ""
        };

        private static readonly string[] firstmem = new string[] {
            "base", ""
        };

        /// This is the current root Action
        private Action? currentact;
        /// The name associated with the current root Action
        private string currentactname;
        /// Map from root Action name to the grouplist it uses
        private Dictionary<string, ActionGroupList> groupmap;
        /// Map from name to root Action
        private Dictionary<string, Action> actionmap;
        /// \b true if only the default groups are set
        private bool isDefaultGroups;
        /// The name of the \e universal root Action
        private const string universalname = "universal";

        /// Register a \e root Action
        /// Internal method for associated a \e root Action name with its Action object.
        /// The database takes over memory management of the object.
        /// \param nm is the name to register as
        /// \param act is the Action object
        private void registerAction(string nm, Action act)
        {
            Action? foundAction;

            if (actionmap.TryGetValue(nm, out foundAction)) {
                // delete foundAction;
            }
            actionmap[nm] = act;
        }

        /// Set up descriptions of preconfigured root Actions
        /// (Re)build the default \e root Actions: decompile, jumptable, normalize, paramid, register, firstpass
        private void buildDefaultGroups()
        {
            if (isDefaultGroups) return;
            groupmap.Clear();
            setGroup("decompile", members);
            setGroup("jumptable", jumptab);
            setGroup("normalize", normali);
            setGroup("paramid", paramid);
            setGroup("register", regmemb);
            setGroup("firstpass", firstmem);
            isDefaultGroups = true;
        }

        /// Look up a \e root Action by name
        /// \param nm is the name of the \e root Action
        private Action? getAction(string nm)
        {
            Action? result;
            if (!actionmap.TryGetValue(nm, out result)) {
                throw new LowlevelError($"No registered action: {nm}");
            }
            return result;
        }

        /// Derive a \e root Action
        /// Internal method to build the Action object corresponding to a \e root Action
        /// The new Action object is created by selectively cloning components
        /// from an existing object based on a grouplist.
        /// \param baseaction is the name of the model Action object to derive \e from
        /// \param grp is the name of the grouplist steering the clone
        private Action deriveAction(string baseaction, string grp)
        {
            Action? foundAction;
            if (actionmap.TryGetValue(grp, out foundAction)) {
                // Already derived this action
                return foundAction;
            }

            // Group should already exist
            ActionGroupList curgrp = getGroup(grp);
            Action act = getAction(baseaction);
            Action newact = act.clone(curgrp);

            // Register the action with the name of the group it was derived from
            registerAction(grp, newact);
            return newact;
        }

        /// Constructor
        public ActionDatabase()
        {
            currentact = null;
            isDefaultGroups = false;
        }

        /// Destructor
        ~ActionDatabase()
        {
            foreach (KeyValuePair<string, Action> iter in actionmap) {
                // delete iter.Value;
            }
        }

        /// (Re)set the default configuration
        /// Clear out (possibly altered) root Actions. Reset the default groups.
        /// Set the default root action "decompile"
        public void resetDefaults()
        {
            Action? universalAction = null;
            actionmap.TryGetValue(universalname, out universalAction);
            foreach (Action curAction in actionmap.Values) {
                if (curAction != universalAction) {
                    // Clear out any old (modified) root actions
                    // delete curAction;
                }
            }
            actionmap.Clear();
            registerAction(universalname, universalAction);
            buildDefaultGroups();
            // The default root action
            setCurrent("decompile");
        }

        /// Get the current \e root Action
        public Action? getCurrent() => currentact;

        /// Get the name of the current \e root Action
        public string getCurrentName() => currentactname;

        /// Get a specific grouplist by name
        public ActionGroupList getGroup(string grp)
        {
            ActionGroupList? result;

            if (!groupmap.TryGetValue(grp, out result)) {
                throw new LowlevelError($"Action group does not exist: {grp}");
            }
            return result;
        }

        /// Set the current \e root Action
        /// The Action is specified by name.  A grouplist must already exist for this name.
        /// If the Action doesn't already exist, it will be derived from the \e universal
        /// action via this grouplist.
        /// \param actname is the name of the \e root Action
        public Action setCurrent(string actname)
        {
            currentactname = actname;
            currentact = deriveAction(universalname, actname);
            return currentact;
        }

        /// Toggle a group of Actions with a \e root Action
        /// A particular group is either added or removed from the grouplist defining
        /// a particular \e root Action.  The \e root Action is then (re)derived from the universal
        /// \param grp is the name of the \e root Action
        /// \param basegrp is name of group (within the grouplist) to toggle
        /// \param val is \b true if the group should be added or \b false if it should be removed
        /// \return the modified \e root Action
        public Action toggleAction(string grp, string basegrp, bool val)
        {
            Action act = getAction(universalname);
            if (val) {
                addToGroup(grp, basegrp);
            }
            else {
                removeFromGroup(grp, basegrp);
            }
            // Group should already exist
            ActionGroupList curgrp = getGroup(grp);
            Action newact = act.clone(curgrp);

            registerAction(grp, newact);

            if (grp == currentactname) {
                currentact = newact;
            }
            return newact;
        }

        /// Establish a new \e root Action
        /// (Re)set the grouplist for a particular \e root Action.  Do not use this routine
        /// to redefine an existing \e root Action.
        /// \param grp is the name of the \e root Action
        /// \param argv is a list of static char pointers, which must end with a NULL pointer, or a zero length string.
        public void setGroup(string grp, string[] argv)
        {
            ActionGroupList curgrp = groupmap[grp];
            // Clear out any old members
            curgrp.list.Clear();
            for (int i = 0; i <= argv.Length; ++i) {
                if (argv[i] == null) {
                    break;
                }
                if (argv[i][0] == '\0') {
                    break;
                }
                curgrp.list.Add(argv[i]);
            }
            isDefaultGroups = false;
        }

        /// Clone a \e root Action
        /// Copy an existing \e root Action by copying its grouplist, giving it a new name.
        /// This is suitable for a copy then modify strategy to create a new \e root Action.
        /// Do not use to redefine a \e root Action that has already been instantiated
        /// \param oldname is the name of an existing \e root Action
        /// \param newname is the name of the copy
        public void cloneGroup(string oldname, string newname)
        {
            // Should already exist
            ActionGroupList curgrp = getGroup(oldname);
            // Copy the group
            groupmap[newname] = curgrp;
            isDefaultGroups = false;
        }

        /// Add a group to a \e root Action
        /// Add a group to the grouplist for a particular \e root Action.
        /// Do not use to redefine a \e root Action that has already been instantiated.
        /// \param grp is the name of the \e root Action
        /// \param basegroup is the group to add
        /// \return \b true for a new addition, \b false is the group was already present
        public bool addToGroup(string grp, string basegroup)
        {
            isDefaultGroups = false;
            return groupmap[grp].list.Add(basegroup);
        }

        /// Remove a group from a \e root Action
        /// The group is removed from the grouplist of a \e root Action.
        /// Do not use to redefine a \e root Action that has already been instantiated.
        /// \param grp is the name of the \e root Action
        /// \param basegrp is the group to remove
        /// \return \b true if the group existed and was removed
        public bool removeFromGroup(string grp, string basegroup)
        {
            isDefaultGroups = false;
            return groupmap[grp].list.Remove(basegroup);
        }

        /// Build the universal action
        /// Construct the \b universal Action that contains all possible components
        /// \param conf is the Architecture that will use the Action
        public void universalAction(Architecture conf)
        {
            IEnumerator<Rule> iter;
            ActionGroup act;
            ActionGroup actmainloop;
            ActionGroup actfullloop;
            ActionPool actprop, actprop2;
            ActionPool actcleanup;
            ActionGroup actstackstall;
            AddrSpace stackspace = conf.getStackSpace();

            act = new ActionRestartGroup(Action.ruleflags.rule_onceperfunc, "universal", 1);
            registerAction(universalname, act);

            act.addAction(new ActionStart("base"));
            act.addAction(new ActionConstbase("base"));
            act.addAction(new ActionNormalizeSetup("normalanalysis"));
            act.addAction(new ActionDefaultParams("base"));
            //  act.addAction( new ActionParamShiftStart("paramshift") );
            act.addAction(new ActionExtraPopSetup("base", stackspace));
            act.addAction(new ActionPrototypeTypes("protorecovery"));
            act.addAction(new ActionFuncLink("protorecovery"));
            act.addAction(new ActionFuncLinkOutOnly("noproto"));
                
            actfullloop = new ActionGroup(Action.ruleflags.rule_repeatapply, "fullloop");

            actmainloop = new ActionGroup(Action.ruleflags.rule_repeatapply, "mainloop");
            actmainloop.addAction(new ActionUnreachable("base"));
            actmainloop.addAction(new ActionVarnodeProps("base"));
            actmainloop.addAction(new ActionHeritage("base"));
            actmainloop.addAction(new ActionParamDouble("protorecovery"));
            actmainloop.addAction(new ActionSegmentize("base"));
            actmainloop.addAction(new ActionForceGoto("blockrecovery"));
            actmainloop.addAction(new ActionDirectWrite("protorecovery_a", true));
            actmainloop.addAction(new ActionDirectWrite("protorecovery_b", false));
            actmainloop.addAction(new ActionActiveParam("protorecovery"));
            actmainloop.addAction(new ActionReturnRecovery("protorecovery"));
            // actmainloop.addAction( new ActionParamShiftStop("paramshift") );
            // Do before dead code removed
            actmainloop.addAction(new ActionRestrictLocal("localrecovery"));
            actmainloop.addAction(new ActionDeadCode("deadcode"));
            // Must come before restructurevarnode and infertypes
            actmainloop.addAction(new ActionDynamicMapping("dynamic"));
            actmainloop.addAction(new ActionRestructureVarnode("localrecovery"));
            // Must come before infertypes and nonzeromask
            actmainloop.addAction(new ActionSpacebase("base"));
            actmainloop.addAction(new ActionNonzeroMask("analysis"));
            actmainloop.addAction(new ActionInferTypes("typerecovery"));
            actstackstall = new ActionGroup(Action.ruleflags.rule_repeatapply, "stackstall");
            actprop = new ActionPool(Action.ruleflags.rule_repeatapply, "oppool1");
            actprop.addRule(new RuleEarlyRemoval("deadcode"));
            actprop.addRule(new RuleTermOrder("analysis"));
            actprop.addRule(new RuleSelectCse("analysis"));
            actprop.addRule(new RuleCollectTerms("analysis"));
            actprop.addRule(new RulePullsubMulti("analysis"));
            actprop.addRule(new RulePullsubIndirect("analysis"));
            actprop.addRule(new RulePushMulti("nodejoin"));
            actprop.addRule(new RuleSborrow("analysis"));
            actprop.addRule(new RuleIntLessEqual("analysis"));
            actprop.addRule(new RuleTrivialArith("analysis"));
            actprop.addRule(new RuleTrivialBool("analysis"));
            actprop.addRule(new RuleTrivialShift("analysis"));
            actprop.addRule(new RuleSignShift("analysis"));
            actprop.addRule(new RuleTestSign("analysis"));
            actprop.addRule(new RuleIdentityEl("analysis"));
            actprop.addRule(new RuleOrMask("analysis"));
            actprop.addRule(new RuleAndMask("analysis"));
            actprop.addRule(new RuleOrConsume("analysis"));
            actprop.addRule(new RuleOrCollapse("analysis"));
            actprop.addRule(new RuleAndOrLump("analysis"));
            actprop.addRule(new RuleShiftBitops("analysis"));
            actprop.addRule(new RuleRightShiftAnd("analysis"));
            actprop.addRule(new RuleNotDistribute("analysis"));
            actprop.addRule(new RuleHighOrderAnd("analysis"));
            actprop.addRule(new RuleAndDistribute("analysis"));
            actprop.addRule(new RuleAndCommute("analysis"));
            actprop.addRule(new RuleAndPiece("analysis"));
            actprop.addRule(new RuleAndZext("analysis"));
            actprop.addRule(new RuleAndCompare("analysis"));
            actprop.addRule(new RuleDoubleSub("analysis"));
            actprop.addRule(new RuleDoubleShift("analysis"));
            actprop.addRule(new RuleDoubleArithShift("analysis"));
            actprop.addRule(new RuleConcatShift("analysis"));
            actprop.addRule(new RuleLeftRight("analysis"));
            actprop.addRule(new RuleShiftCompare("analysis"));
            actprop.addRule(new RuleShift2Mult("analysis"));
            actprop.addRule(new RuleShiftPiece("analysis"));
            actprop.addRule(new RuleMultiCollapse("analysis"));
            actprop.addRule(new RuleIndirectCollapse("analysis"));
            actprop.addRule(new Rule2Comp2Mult("analysis"));
            actprop.addRule(new RuleSub2Add("analysis"));
            actprop.addRule(new RuleCarryElim("analysis"));
            actprop.addRule(new RuleBxor2NotEqual("analysis"));
            actprop.addRule(new RuleLess2Zero("analysis"));
            actprop.addRule(new RuleLessEqual2Zero("analysis"));
            actprop.addRule(new RuleSLess2Zero("analysis"));
            actprop.addRule(new RuleEqual2Zero("analysis"));
            actprop.addRule(new RuleEqual2Constant("analysis"));
            actprop.addRule(new RuleThreeWayCompare("analysis"));
            actprop.addRule(new RuleXorCollapse("analysis"));
            actprop.addRule(new RuleAddMultCollapse("analysis"));
            actprop.addRule(new RuleCollapseConstants("analysis"));
            actprop.addRule(new RuleTransformCpool("analysis"));
            actprop.addRule(new RulePropagateCopy("analysis"));
            actprop.addRule(new RuleZextEliminate("analysis"));
            actprop.addRule(new RuleSlessToLess("analysis"));
            actprop.addRule(new RuleZextSless("analysis"));
            actprop.addRule(new RuleBitUndistribute("analysis"));
            actprop.addRule(new RuleBoolZext("analysis"));
            actprop.addRule(new RuleBooleanNegate("analysis"));
            actprop.addRule(new RuleLogic2Bool("analysis"));
            actprop.addRule(new RuleSubExtComm("analysis"));
            actprop.addRule(new RuleSubCommute("analysis"));
            actprop.addRule(new RuleConcatCommute("analysis"));
            actprop.addRule(new RuleConcatZext("analysis"));
            actprop.addRule(new RuleZextCommute("analysis"));
            actprop.addRule(new RuleZextShiftZext("analysis"));
            actprop.addRule(new RuleShiftAnd("analysis"));
            actprop.addRule(new RuleConcatZero("analysis"));
            actprop.addRule(new RuleConcatLeftShift("analysis"));
            actprop.addRule(new RuleSubZext("analysis"));
            actprop.addRule(new RuleSubCancel("analysis"));
            actprop.addRule(new RuleShiftSub("analysis"));
            actprop.addRule(new RuleHumptyDumpty("analysis"));
            actprop.addRule(new RuleDumptyHump("analysis"));
            actprop.addRule(new RuleHumptyOr("analysis"));
            actprop.addRule(new RuleNegateIdentity("analysis"));
            actprop.addRule(new RuleSubNormal("analysis"));
            actprop.addRule(new RulePositiveDiv("analysis"));
            actprop.addRule(new RuleDivTermAdd("analysis"));
            actprop.addRule(new RuleDivTermAdd2("analysis"));
            actprop.addRule(new RuleDivOpt("analysis"));
            actprop.addRule(new RuleSignForm("analysis"));
            actprop.addRule(new RuleSignForm2("analysis"));
            actprop.addRule(new RuleSignDiv2("analysis"));
            actprop.addRule(new RuleDivChain("analysis"));
            actprop.addRule(new RuleSignNearMult("analysis"));
            actprop.addRule(new RuleModOpt("analysis"));
            actprop.addRule(new RuleSignMod2nOpt("analysis"));
            actprop.addRule(new RuleSignMod2nOpt2("analysis"));
            actprop.addRule(new RuleSignMod2Opt("analysis"));
            actprop.addRule(new RuleSwitchSingle("analysis"));
            actprop.addRule(new RuleCondNegate("analysis"));
            actprop.addRule(new RuleBoolNegate("analysis"));
            actprop.addRule(new RuleLessEqual("analysis"));
            actprop.addRule(new RuleLessNotEqual("analysis"));
            actprop.addRule(new RuleLessOne("analysis"));
            actprop.addRule(new RuleRangeMeld("analysis"));
            actprop.addRule(new RuleFloatRange("analysis"));
            actprop.addRule(new RulePiece2Zext("analysis"));
            actprop.addRule(new RulePiece2Sext("analysis"));
            actprop.addRule(new RulePopcountBoolXor("analysis"));
            actprop.addRule(new RuleOrMultiBool("analysis"));
            actprop.addRule(new RuleXorSwap("analysis"));
            actprop.addRule(new RuleLzcountShiftBool("analysis"));
            actprop.addRule(new RuleSubvarAnd("subvar"));
            actprop.addRule(new RuleSubvarSubpiece("subvar"));
            actprop.addRule(new RuleSplitFlow("subvar"));
            actprop.addRule(new RulePtrFlow("subvar", conf));
            actprop.addRule(new RuleSubvarCompZero("subvar"));
            actprop.addRule(new RuleSubvarShift("subvar"));
            actprop.addRule(new RuleSubvarZext("subvar"));
            actprop.addRule(new RuleSubvarSext("subvar"));
            actprop.addRule(new RuleNegateNegate("analysis"));
            actprop.addRule(new RuleConditionalMove("conditionalexe"));
            actprop.addRule(new RuleOrPredicate("conditionalexe"));
            actprop.addRule(new RuleFuncPtrEncoding("analysis"));
            actprop.addRule(new RuleSubfloatConvert("floatprecision"));
            actprop.addRule(new RuleFloatCast("floatprecision"));
            actprop.addRule(new RuleIgnoreNan("floatprecision"));
            actprop.addRule(new RulePtraddUndo("typerecovery"));
            actprop.addRule(new RulePtrsubUndo("typerecovery"));
            actprop.addRule(new RuleSegment("segment"));
            actprop.addRule(new RulePiecePathology("protorecovery"));

            actprop.addRule(new RuleDoubleLoad("doubleload"));
            actprop.addRule(new RuleDoubleStore("doubleprecis"));
            actprop.addRule(new RuleDoubleIn("doubleprecis"));
            foreach (Rule rule in conf.extra_pool_rules)
                // Add CPU specific rules
                actprop.addRule(rule);
            // Rules are now absorbed into universal
            conf.extra_pool_rules.Clear();
            actstackstall.addAction(actprop);
            actstackstall.addAction(new ActionLaneDivide("base"));
            actstackstall.addAction(new ActionMultiCse("analysis"));
            actstackstall.addAction(new ActionShadowVar("analysis"));
            actstackstall.addAction(new ActionDeindirect("deindirect"));
            actstackstall.addAction(new ActionStackPtrFlow("stackptrflow", stackspace));
            actmainloop.addAction(actstackstall);
            // dead code removal
            actmainloop.addAction(new ActionRedundBranch("deadcontrolflow"));
            actmainloop.addAction(new ActionBlockStructure("blockrecovery"));
            actmainloop.addAction(new ActionConstantPtr("typerecovery"));

            actprop2 = new ActionPool(Action.ruleflags.rule_repeatapply, "oppool2");

            actprop2.addRule(new RulePushPtr("typerecovery"));
            actprop2.addRule(new RuleStructOffset0("typerecovery"));
            actprop2.addRule(new RulePtrArith("typerecovery"));
            //	actprop2.addRule( new RuleIndirectConcat("analysis") );
            actprop2.addRule(new RuleLoadVarnode("stackvars"));
            actprop2.addRule(new RuleStoreVarnode("stackvars"));

            actmainloop.addAction(actprop2);
            actmainloop.addAction(new ActionDeterminedBranch("unreachable"));
            actmainloop.addAction(new ActionUnreachable("unreachable"));
            actmainloop.addAction(new ActionNodeJoin("nodejoin"));
            actmainloop.addAction(new ActionConditionalExe("conditionalexe"));
            actmainloop.addAction(new ActionConditionalConst("analysis"));

            actfullloop.addAction(actmainloop);
            actfullloop.addAction(new ActionLikelyTrash("protorecovery"));
            actfullloop.addAction(new ActionDirectWrite("protorecovery_a", true));
            actfullloop.addAction(new ActionDirectWrite("protorecovery_b", false));
            actfullloop.addAction(new ActionDeadCode("deadcode"));
            actfullloop.addAction(new ActionDoNothing("deadcontrolflow"));
            actfullloop.addAction(new ActionSwitchNorm("switchnorm"));
            actfullloop.addAction(new ActionReturnSplit("returnsplit"));
            actfullloop.addAction(new ActionUnjustifiedParams("protorecovery"));
            actfullloop.addAction(new ActionStartTypes("typerecovery"));
            actfullloop.addAction(new ActionActiveReturn("protorecovery"));

            act.addAction(actfullloop);
            act.addAction(new ActionStartCleanUp("cleanup"));

            actcleanup = new ActionPool(Action.ruleflags.rule_repeatapply, "cleanup");

            actcleanup.addRule(new RuleMultNegOne("cleanup"));
            actcleanup.addRule(new RuleAddUnsigned("cleanup"));
            actcleanup.addRule(new Rule2Comp2Sub("cleanup"));
            actcleanup.addRule(new RuleSubRight("cleanup"));
            actcleanup.addRule(new RulePtrsubCharConstant("cleanup"));
            actcleanup.addRule(new RuleExtensionPush("cleanup"));
            actcleanup.addRule(new RulePieceStructure("cleanup"));
            actcleanup.addRule(new RuleSplitCopy("splitcopy"));
            actcleanup.addRule(new RuleSplitLoad("splitpointer"));
            actcleanup.addRule(new RuleSplitStore("splitpointer"));

            act.addAction(actcleanup);

            act.addAction(new ActionPreferComplement("blockrecovery"));
            act.addAction(new ActionStructureTransform("blockrecovery"));
            act.addAction(new ActionNormalizeBranches("normalizebranches"));
            act.addAction(new ActionAssignHigh("merge"));
            act.addAction(new ActionMergeRequired("merge"));
            act.addAction(new ActionMarkExplicit("merge"));
            // This must come BEFORE general merging
            act.addAction(new ActionMarkImplied("merge"));
            act.addAction(new ActionMergeMultiEntry("merge"));
            act.addAction(new ActionMergeCopy("merge"));
            act.addAction(new ActionDominantCopy("merge"));
            act.addAction(new ActionDynamicSymbols("dynamic"));
            // Must come after required merges but before speculative
            act.addAction(new ActionMarkIndirectOnly("merge"));
            act.addAction(new ActionMergeAdjacent("merge"));
            act.addAction(new ActionMergeType("merge"));
            act.addAction(new ActionHideShadow("merge"));
            act.addAction(new ActionCopyMarker("merge"));
            act.addAction(new ActionOutputPrototype("localrecovery"));
            act.addAction(new ActionInputPrototype("fixateproto"));
            act.addAction(new ActionRestructureHigh("localrecovery"));
            act.addAction(new ActionMapGlobals("fixateglobals"));
            act.addAction(new ActionDynamicSymbols("dynamic"));
            act.addAction(new ActionNameVars("merge"));
            act.addAction(new ActionSetCasts("casts"));
            act.addAction(new ActionFinalStructure("blockrecovery"));
            act.addAction(new ActionPrototypeWarnings("protorecovery"));
            act.addAction(new ActionStop("base"));
        }
    }
}
