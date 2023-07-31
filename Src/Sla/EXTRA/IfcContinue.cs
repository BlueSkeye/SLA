using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcContinue : IfaceDecompCommand
    {
        /// \class IfcContinue
        /// \brief Continue decompilation after a break point: `continue`
        ///
        /// This command assumes decompilation has been started and has hit a break point.
        public override void execute(TextReader s)
        {
            int res;

            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Decompile action not loaded");

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            if (dcp.conf.allacts.getCurrent().getStatus() == Action::status_start)
                throw new IfaceExecutionError("Decompilation has not been started");
            if (dcp.conf.allacts.getCurrent().getStatus() == Action::status_end)
                throw new IfaceExecutionError("Decompilation is already complete");

            res = dcp.conf.allacts.getCurrent().perform(*dcp.fd); // Try to continue decompilation
            if (res < 0)
            {
                *status.optr << "Break at ";
                dcp.conf.allacts.getCurrent().printState(*status.optr);
            }
            else
            {
                *status.optr << "Decompilation complete";
                if (res == 0)
                    *status.optr << " (no change)";
            }
            *status.optr << endl;
        }
    }
}
