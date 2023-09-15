using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcPrintActionstats : IfaceDecompCommand
    {
        /// \class IfcPrintActionstats
        /// \brief Print transform statistics for the decompiler engine: `print actionstats`
        ///
        /// Counts for each Action and Rule are displayed; showing the number of attempts,
        /// both successful and not, that were made to apply each one.  Counts can accumulate
        /// over multiple decompilations.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Image not loaded");
            if (dcp.conf.allacts.getCurrent() == (Action*)0)
                throw new IfaceExecutionError("No action set");

            dcp.conf.allacts.getCurrent().printStatistics(status.fileoptr);
        }
    }
}
