﻿using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintHigh : IfaceDecompCommand
    {
        /// \class IfcPrintHigh
        /// \brief Display all Varnodes in a HighVariable: `print high <name>`
        ///
        /// A HighVariable associated with the current function is specified by name.
        /// Information about every Varnode merged into the variable is displayed.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null) {
                throw new IfaceExecutionError("No function selected");
            }
            string varname = s.ReadString();
            s.ReadSpaces();
            HighVariable? high = dcp.fd.findHigh(varname);
            if (high == (HighVariable)null) {
                // Didn't find this name
                throw new IfaceExecutionError("Unknown variable name: " + varname);
            }
            high.printInfo(status.optr);
        }
    }
}
