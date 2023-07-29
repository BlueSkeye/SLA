using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintCover : IfaceDecompCommand
    {
        /// \class IfcPrintCover
        /// \brief Print cover info about a HighVariable: `print cover high <name>`
        ///
        /// A HighVariable is specified by its symbol name in the current function's scope.
        /// Information about the code ranges where the HighVariable is in scope is printed.
        public override void execute(TextReader s)
        {
            HighVariable* high;
            string name;

            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            s >> ws >> name;
            if (name.size() == 0)
                throw IfaceParseError("Missing variable name");
            high = dcp->fd->findHigh(name);
            if (high == (HighVariable*)0)
                throw IfaceExecutionError("Unable to find variable: " + name);

            high->printCover(*status->optr);
        }
    }
}
