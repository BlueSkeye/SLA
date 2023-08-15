using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcGraphControlflow : IfaceDecompCommand
    {
        /// \class IfcGraphControlflow
        /// \brief Write a graph representation of control-flow to a file: `graph controlflow <filename>`
        ///
        /// The control-flow graph for the \e current function, in its current state of transform,
        /// is written to the indicated file.
        public override void execute(TextReader s)
        {
            string filename;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            s >> filename;
            if (filename.Length == 0)
                throw new IfaceParseError("Missing output file");
            if (dcp.fd.getBasicBlocks().getSize() == 0)
                throw new IfaceExecutionError("Basic block structure not calculated");
            StreamWriter thefile;

            try { thefile = new StreamWriter(File.OpenWrite(filename)); }
            catch {
                throw new IfaceExecutionError($"Unable to open output file: {filename}");
            }

            dump_controlflow_graph(dcp.fd.getName(), dcp.fd.getBasicBlocks(), thefile);
            thefile.Close();
        }

        private static void dump_controlflow_graph(string name, BlockGraph graph, TextWriter s)
        {
            s.WriteLine($"*CMD=NewGraphWindow, WindowName={name}-controlflow;");
            s.WriteLine($"*CMD=*NEXUS,Name={name}-controlflow;");
            Graph.dump_block_properties(s);
            Graph.dump_block_attributes(s);
            Graph.dump_block_vertex(graph, s, false);
            dump_block_edges(graph, s);
        }
    }
}
