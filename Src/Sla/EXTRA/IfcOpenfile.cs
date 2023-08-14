using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcOpenfile : IfaceBaseCommand
    {
        /// \class IfcOpenfile
        /// \brief Open file command to redirect bulk output to a specific file stream
        public override void execute(TextReader s)
        {
            string filename;

            if (status.optr != status.fileoptr)
                throw new IfaceExecutionError("Output file already opened");
            s >> filename;
            if (filename.empty())
                throw new IfaceParseError("No filename specified");

            try { status.fileoptr = new StreamWriter(File.OpenWrite(filename)); }
            catch {
                // delete status.fileoptr;
                status.fileoptr = status.optr;
                throw new IfaceExecutionError("Unable to open file: " + filename);
            }
        }
    }
}
