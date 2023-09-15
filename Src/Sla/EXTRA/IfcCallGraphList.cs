using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCallGraphList : IfaceDecompCommand
    {
        /// \class IfcCallGraphList
        /// \brief List all functions in \e leaf order: `callgraph list`
        ///
        /// The existing call-graph is walked, displaying function names to the console.
        /// Child functions are displayed before their parents.
        public override void execute(TextReader s)
        {
            if (dcp.cgraph == (CallGraph)null)
                throw new IfaceExecutionError("Callgraph not generated");

            iterateFunctionsLeafOrder();
        }

        public override void iterationCallback(Funcdata fd)
        {
            status.optr.WriteLine(fd.getName());
        }
    }
}
