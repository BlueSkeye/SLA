using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief Groups of console commands that are \e discovered by the loader
    ///
    /// Any IfaceCommand that is registered with a grouping derived from this class
    /// is automatically made available to any IfaceStatus object just by calling
    /// the static registerAllCommands()
    internal abstract class IfaceCapability : CapabilityPoint
    {
        /// The global list of discovered command groupings
        private static List<IfaceCapability> thelist = new List<IfaceCapability>();

        /// Identifying name for the capability
        protected string name;

        /// Get the name of the capability
        public string getName() => name; 
    
        public override void initialize()
        {
            thelist.Add(this);
        }

        public abstract void registerCommands(IfaceStatus status);

        /// Register all discovered commands with the interface
        /// Register commands for \b this grouping
        /// Allow each capability to register its own commands
        ///
        /// \param status is the command line interface to register commands with
        public static void registerAllCommands(IfaceStatus status)
        {
            for (uint i = 0; i < thelist.size(); ++i)
                thelist[i].registerCommands(status);
        }
    }
}
