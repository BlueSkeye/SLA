using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief A root class for a basic set of commands
    ///
    /// Commands derived from this class are in the "base" module.
    /// They are useful as part of any interface
    internal class IfaceBaseCommand : IfaceCommand
    {
        protected IfaceStatus status;      ///< The interface owning this command instance
        
        public override void setData(IfaceStatus root, IfaceData data)
        {
            status = root;
        }
        
        public override string getModule() => "base";

        public override IfaceData createData()
        {
            return (IfaceData*)0;
        }
    }
}
