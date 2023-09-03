using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcPrintRaw : IfaceDecompCommand
    {
        /// \class IfcPrintRaw
        /// \brief Print the raw p-code for the \e current function: `print raw`
        ///
        /// Each p-code op, in its present state, is printed to the console, labeled
        /// with the address of its original instruction and any output and input varnodes.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");
            dcp.fd.printRaw(status.fileoptr);
        }
    }
}
