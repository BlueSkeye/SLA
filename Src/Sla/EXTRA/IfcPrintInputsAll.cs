using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintInputsAll : IfaceDecompCommand
    {
        /// \class IfcPrintInputsAll
        /// \brief Print info about input Varnodes for all functions: `print inputs all`
        ///
        /// Each function is decompiled, and info about its input Varnodes are printed.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            iterateFunctionsAddrOrder();
        }

        public void iterationCallback(Funcdata fd)
        {
            if (fd.hasNoCode())
            {
                *status.optr << "No code for " << fd.getName() << endl;
                return;
            }
            try
            {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(*fd);
                dcp.conf.allacts.getCurrent().perform(*fd);
                IfcPrintInputs::print(fd, status.fileoptr);
            }
            catch (LowlevelError err)
            {
                *status.optr << "Skipping " << fd.getName() << ": " << err.ToString() << endl;
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
