using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief The list of groups defining a \e root Action
    ///
    /// Any Rule or \e leaf Action belongs to a \b group. This class
    /// is a \b grouplist defined by a collection of these \b group names.
    /// The set of Rule and Action objects belong to any of the groups in this list
    /// together form a \b root Action.
    internal class ActionGroupList
    {
        // friend class ActionDatabase;
        /// List of group names
        internal HashSet<string> list;
        
        /// \brief Check if \b this ActionGroupList contains a given group
        /// \param nm is the given group to check for
        /// \return true if \b this contains the group
        public bool contains(string nm)
        {
            return (list.Contains(nm));
        }
    }
}
