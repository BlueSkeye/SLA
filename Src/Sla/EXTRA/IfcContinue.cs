using Sla.DECCORE;

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

            if (dcp.conf.allacts.getCurrent().getStatus() == Sla.DECCORE.Action.statusflags.status_start)
                throw new IfaceExecutionError("Decompilation has not been started");
            if (dcp.conf.allacts.getCurrent().getStatus() == Sla.DECCORE.Action.statusflags.status_end)
                throw new IfaceExecutionError("Decompilation is already complete");

            // Try to continue decompilation
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
