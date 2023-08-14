using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcOpenfileAppend : IfaceBaseCommand
    {
        /// \class IfcOpenfileAppend
        /// \brief Open file command directing bulk output to be appended to a specific file
        public override void execute(TextReader s)
        {
            string filename;

            if (status.optr != status.fileoptr)
                throw new IfaceExecutionError("Output file already opened");
            s >> filename;
            if (filename.empty())
                throw new IfaceParseError("No filename specified");

            try { status.fileoptr = new StreamWriter(File.Open(filename, FileMode.Append)); }
            catch {
                // delete status.fileoptr;
                status.fileoptr = status.optr;
                throw new IfaceExecutionError("Unable to open file: " + filename);
            }
        }
    }
}
