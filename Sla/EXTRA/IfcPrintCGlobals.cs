using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintCGlobals : IfaceDecompCommand
    {
        /// \class IfcPrintCGlobals
        /// \brief Print declarations for any known global variables: `print C globals`
        public override void execute(TextReader s)
        {
            if (dcp->conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");

            dcp->conf->print->setOutputStream(status->fileoptr);
            dcp->conf->print->docAllGlobals();
        }
    }
}
