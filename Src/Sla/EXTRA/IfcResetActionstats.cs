using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcResetActionstats : IfaceDecompCommand
    {
        /// \class IfcResetActionstats
        /// \brief Reset transform statistics for the decompiler engine: `reset actionstats`
        ///
        /// Counts for each Action and Rule are reset to zero.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Image not loaded");
            if (dcp.conf.allacts.getCurrent() == (Sla.DECCORE.Action)null)
                throw new IfaceExecutionError("No action set");

            dcp.conf.allacts.getCurrent().resetStats();
        }
    }
}
