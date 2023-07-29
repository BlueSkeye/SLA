using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintCXml : IfaceDecompCommand
    {
        /// \class IfcPrintCXml
        /// \brief Print the current function with C syntax and XML markup:`print C xml`
        public override void execute(TextReader s)
        {
            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            dcp->conf->print->setOutputStream(status->fileoptr);
            dcp->conf->print->setMarkup(true);
            dcp->conf->print->docFunction(dcp->fd);
            dcp->conf->print->setMarkup(false);
        }
    }
}
