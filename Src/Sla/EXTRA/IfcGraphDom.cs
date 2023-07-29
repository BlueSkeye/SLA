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
            string filename;

            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            s >> filename;
            if (filename.size() == 0)
                throw IfaceParseError("Missing output file");
            if (!dcp->fd->isProcStarted())
                throw IfaceExecutionError("Basic block structure not calculated");
            ofstream thefile(filename.c_str());
            if (!thefile)
                throw IfaceExecutionError("Unable to open output file: " + filename);

            dump_dom_graph(dcp->fd->getName(), dcp->fd->getBasicBlocks(), thefile);
            thefile.close();
        }
    }
}
