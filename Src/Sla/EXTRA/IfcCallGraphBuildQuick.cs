using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCallGraphBuildQuick : IfcCallGraphBuild
    {
        /// \class IfcCallGraphBuildQuick
        /// \brief Build the call-graph using quick analysis: `callgraph build quick`
        ///
        /// Build the call-graph for the architecture/program.  For each function, disassembly
        /// is performed to discover call edges, rather then full decompilation.  Some forms
        /// of direct call may not be discovered.
        public override void execute(TextReader s)
        {
            dcp.allocateCallGraph();
            dcp.cgraph.buildAllNodes();   // Build a node in the graph for existing symbols
            quick = true;
            iterateFunctionsAddrOrder();
            *status.optr << "Successfully built callgraph" << endl;
        }
    }
}
