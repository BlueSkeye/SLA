using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcListaction : IfaceDecompCommand
    {
        /// \class IfcListaction
        /// \brief List all current actions and rules for the decompiler: `list action`
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Decompile action not loaded");
            dcp.conf.allacts.getCurrent().print(*status.fileoptr, 0, 0);
        }
    }
}
