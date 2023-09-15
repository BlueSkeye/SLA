using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcPrintCGlobals : IfaceDecompCommand
    {
        /// \class IfcPrintCGlobals
        /// \brief Print declarations for any known global variables: `print C globals`
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            dcp.conf.print.setOutputStream(status.fileoptr);
            dcp.conf.print.docAllGlobals();
        }
    }
}
