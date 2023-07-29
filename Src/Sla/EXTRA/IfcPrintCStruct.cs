using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintCStruct : IfaceDecompCommand
    {
        /// \class IfcPrintCStruct
        /// \brief Print the current function using C syntax:`print C`
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            dcp.conf.print.setOutputStream(status.fileoptr);
            dcp.conf.print.docFunction(dcp.fd);
        }
    }
}
