using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (filename.size() == 0)
                throw new IfaceParseError("Missing output file");
            if (dcp.fd.getBasicBlocks().getSize() == 0)
                throw new IfaceExecutionError("Basic block structure not calculated");
            ofstream thefile(filename.c_str());
            if (!thefile)
                throw new IfaceExecutionError("Unable to open output file: " + filename);

            dump_controlflow_graph(dcp.fd.getName(), dcp.fd.getBasicBlocks(), thefile);
            thefile.close();
        }
    }
}
