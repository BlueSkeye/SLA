using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintCTypes : IfaceDecompCommand
    {
        /// \class IfcPrintCTypes
        /// \brief Print any known type definitions: `print C types`
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw new IfaceExecutionError("No load image present");

            if (dcp.conf.types != (TypeFactory*)0)
            {
                dcp.conf.print.setOutputStream(status.fileoptr);
                dcp.conf.print.docTypeDefinitions(dcp.conf.types);
            }
        }
    }
}
