using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
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
        private void buildDefaultGroups()

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
        public void universalAction(Architecture glb)
    }
}
