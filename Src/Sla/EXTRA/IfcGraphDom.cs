using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcGraphDom : IfaceDecompCommand
    {
        /// \class IfcGraphDom
        /// \brief Write the forward dominance graph to a file: `graph dom <filename>`
        ///
        /// The dominance tree, associated with the control-flow graph of the \e current function
        /// in its current state of transform, is written to the indicated file.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            string filename = s.ReadString();
            if (filename.Length == 0)
                throw new IfaceParseError("Missing output file");
            if (!dcp.fd.isProcStarted())
                throw new IfaceExecutionError("Basic block structure not calculated");
            TextWriter thefile;
            try { thefile = new StreamWriter(File.OpenWrite(filename)); }
            catch {
                throw new IfaceExecutionError("Unable to open output file: " + filename);
            }
            Graph.dump_dom_graph(dcp.fd.getName(), dcp.fd.getBasicBlocks(), thefile);
            thefile.Close();
        }
    }
}
