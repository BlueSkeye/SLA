using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintBlocktree : IfaceDecompCommand
    {
        /// \class IfcPrintBlocktree
        /// \brief Print a description of the \e current functions control-flow: `print tree block`
        ///
        /// The recovered control-flow structure is displayed as a hierarchical list of blocks,
        /// showing the nesting and code ranges covered by the blocks.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            dcp.fd.printBlockTree(status.fileoptr);
        }
    }
}
