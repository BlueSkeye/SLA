using Sla.CORE;
using Sla.DECCORE;

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
            if (fd.hasNoCode()) {
                status.optr.WriteLine($"No code for {fd.getName()}");
                return;
            }
            try {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(fd);
                dcp.conf.allacts.getCurrent().perform(fd);
                IfcPrintInputs::print(fd, status.fileoptr);
            }
            catch (LowlevelError err) {
                status.optr.WriteLine($"Skipping {fd.getName()}: {err.ToString()}");
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
