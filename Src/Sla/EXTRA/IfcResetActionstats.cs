using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (dcp.conf == (Architecture*)0)
                throw new IfaceExecutionError("Image not loaded");
            if (dcp.conf.allacts.getCurrent() == (Action*)0)
                throw new IfaceExecutionError("No action set");

            dcp.conf.allacts.getCurrent().resetStats();
        }
    }
}
