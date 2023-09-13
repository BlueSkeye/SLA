using Sla.CORE;

namespace Sla.EXTRA
{
    internal class IfcCallGraphDump : IfaceDecompCommand
    {
        /// \class IfcCallGraphDump
        /// \brief Write the current call-graph to a file: `callgraph dump <filename>`
        ///
        /// The existing call-graph object is written to the provided file as an
        /// XML document.
        public override void execute(TextReader s)
        {
            if (dcp.cgraph == (CallGraph)null)
                throw new IfaceExecutionError("No callgraph has been built");

            string name;
            s.ReadSpaces() >> name;
            if (name.size() == 0)
                throw new IfaceParseError("Need file name to write callgraph to");

            TextWriter os;
            try { os = new StreamWriter(File.OpenWrite(name)); }
            catch {
                throw new IfaceExecutionError($"Unable to open file {name}");
            }
            XmlEncode encoder = new XmlEncode(os);
            dcp.cgraph.encode(encoder);
            os.close();
            status.optr.WriteLine($"Successfully saved callgraph to {name}");
        }
    }
}
