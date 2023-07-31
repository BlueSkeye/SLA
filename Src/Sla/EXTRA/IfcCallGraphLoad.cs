using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCallGraphLoad : IfaceDecompCommand
    {
        /// \class IfcCallGraphLoad
        /// \brief Load the call-graph from a file: `callgraph load <filename>`
        ///
        /// A call-graph is loaded from the provided XML document.  Nodes in the
        /// call-graph are linked to existing functions by symbol name.  This command
        /// reports call-graph nodes that could not be linked.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Decompile action not loaded");
            if (dcp.cgraph != (CallGraph)null)
                throw new IfaceExecutionError("Callgraph already loaded");

            string name;

            s >> ws >> name;
            if (name.size() == 0)
                throw new IfaceExecutionError("Need name of file to read callgraph from");

            ifstream @is = new ifstream(name.c_str());
            if (!@is)
                throw new IfaceExecutionError("Unable to open callgraph file " + name);

            DocumentStorage store;
            Document doc = store.parseDocument(@is);

            dcp.allocateCallGraph();
            XmlDecode decoder(dcp.conf, doc.getRoot());
            dcp.cgraph.decoder(decoder);
            *status.optr << "Successfully read in callgraph" << endl;

            Scope gscope = dcp.conf.symboltab.getGlobalScope();
            Dictionary<Address, CallGraphNode>.Enumerator iter = dcp.cgraph.begin();

            while(iter.MoveNext()) { 
                CallGraphNode node = iter.Current.Value;
                Funcdata fd = gscope.queryFunction(node.getName());
                if (fd == (Funcdata)null)
                    throw new IfaceExecutionError(
                        $"Function:{node.getName()} in callgraph has not been loaded");
                node.setFuncdata(fd);
            }

            *status.optr << "Successfully associated functions with callgraph nodes" << endl;
        }
    }
}
