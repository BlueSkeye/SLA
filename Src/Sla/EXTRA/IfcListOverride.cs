﻿using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcListOverride : IfaceDecompCommand
    {
        /// \class IfcListOverride
        /// \brief Display any overrides for the current function: `list override`
        ///
        /// Overrides include:
        ///   - Forced gotos
        ///   - Dead code delays
        ///   - Indirect call overrides
        ///   - Indirect prototype overrides
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            status.optr.WriteLine($"No code for {dcp.fd.getName()}");
            dcp.fd.getOverride().printRaw(*status.optr, dcp.conf);
        }
    }
}
