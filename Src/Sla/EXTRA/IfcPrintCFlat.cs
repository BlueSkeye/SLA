using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintCFlat : IfaceDecompCommand
    {
        /// \class IfcPrintCFlat
        /// \brief Print current function without control-flow: `print C flat`
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            dcp.conf.print.setOutputStream(status.fileoptr);
            dcp.conf.print.setFlat(true);
            dcp.conf.print.docFunction(dcp.fd);
            dcp.conf.print.setFlat(false);
        }
    }
}
