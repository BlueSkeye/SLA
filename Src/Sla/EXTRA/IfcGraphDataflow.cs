using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcGraphDataflow : IfaceDecompCommand
    {
        /// \class IfcGraphDataflow
        /// \brief Write a graph representation of data-flow to a file: `graph dataflow <filename>`
        ///
        /// The data-flow graph for the \e current function, in its current state of transform,
        /// is written to the indicated file.
        public override void execute(TextReader s)
        {
            string filename;

            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            s >> filename;
            if (filename.size() == 0)
                throw IfaceParseError("Missing output file");
            if (!dcp->fd->isProcStarted())
                throw IfaceExecutionError("Syntax tree not calculated");
            ofstream thefile(filename.c_str());
            if (!thefile)
                throw IfaceExecutionError("Unable to open output file: " + filename);

            dump_dataflow_graph(*dcp->fd, thefile);
            thefile.close();
        }
    }
}
