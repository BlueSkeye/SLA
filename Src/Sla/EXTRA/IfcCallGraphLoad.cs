﻿using Sla.CORE;
using Sla.DECCORE;

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

            s.ReadSpaces();
            string name = s.ReadString();
            if (name.Length == 0)
                throw new IfaceExecutionError("Need name of file to read callgraph from");

            TextReader @is;
            try { @is = new StreamReader(File.OpenRead(name)); }
            catch {
                throw new IfaceExecutionError($"Unable to open callgraph file {name}");
            }

            DocumentStorage store = new DocumentStorage();
            Document doc = store.parseDocument(@is);

            dcp.allocateCallGraph();
            XmlDecode decoder = new XmlDecode(dcp.conf, doc.getRoot());
            dcp.cgraph.decoder(decoder);
            status.optr.WriteLine("Successfully read in callgraph");

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

            status.optr .WriteLine("Successfully associated functions with callgraph nodes");
        }
    }
}
