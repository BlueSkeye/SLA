using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcDecompile : IfaceDecompCommand
    {
        /// \class IfcDecompile
        /// \brief Decompile the current function: `decompile`
        ///
        /// Decompilation is started for the current function. Any previous decompilation
        /// analysis on the function is cleared first.  The process respects
        /// any active break points or traces, so decompilation may not complete.
        public override void execute(TextReader s)
        {
            int res;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            if (dcp.fd.hasNoCode()) {
                status.optr.WriteLine($"No code for {dcp.fd.getName()}");
                return;
            }
            if (dcp.fd.isProcStarted()) {
                // Free up old decompile
                status.optr.WriteLine("Clearing old decompilation");
                dcp.conf.clearAnalysis(dcp.fd);
            }

            status.optr.WriteLine($"No code for {dcp.fd.getName()}");
            dcp.conf.allacts.getCurrent().reset(dcp.fd);
            res = dcp.conf.allacts.getCurrent().perform(dcp.fd);
            if (res < 0) {
                status.optr.Write("Break at ");
                dcp.conf.allacts.getCurrent().printState(status.optr);
            }
            else {
                status.optr.Write("Decompilation complete");
                if (res == 0)
                    status.optr.Write(" (no change)");
            }
            status.optr.WriteLine();
        }
    }
}
