using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintLocalrange : IfaceDecompCommand
    {
        /// \class IfcPrintLocalrange
        /// \brief Print range of locals on the stack: `print localrange`
        ///
        /// Print the memory range(s) on the stack is or could be used for
        ///
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            dcp.fd.printLocalRange(*status.optr);
        }
    }
}
