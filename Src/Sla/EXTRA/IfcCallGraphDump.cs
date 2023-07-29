using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (dcp.cgraph == (CallGraph*)0)
                throw new IfaceExecutionError("No callgraph has been built");

            string name;
            s >> ws >> name;
            if (name.size() == 0)
                throw new IfaceParseError("Need file name to write callgraph to");

            ofstream os;
            os.open(name.c_str());
            if (!os)
                throw new IfaceExecutionError("Unable to open file " + name);

            XmlEncode encoder(os);
            dcp.cgraph.encode(encoder);
            os.close();
            *status.optr << "Successfully saved callgraph to " << name << endl;
        }
    }
}
