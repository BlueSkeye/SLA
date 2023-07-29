using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintTree : IfaceDecompCommand
    {
        /// \class IfcPrintTree
        /// \brief Print all Varnodes in the \e current function: `print tree varnode`
        ///
        /// Information about every Varnode in the data-flow graph for the function is displayed.
        public override void execute(TextReader s)
        {
            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            dcp->fd->printVarnodeTree(*status->fileoptr);
        }
    }
}
