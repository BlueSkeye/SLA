using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCleararch : IfaceDecompCommand
    {
        /// \class IfcCleararch
        /// \brief Clear the current architecture/program: `clear architecture`
        public override void execute(TextReader s)
        {
            dcp.clearArchitecture();
        }
    }
}
