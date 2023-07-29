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
                throw IfaceExecutionError("Output file already opened");
            s >> filename;
            if (filename.empty())
                throw IfaceParseError("No filename specified");

            status.fileoptr = new ofstream;
            ((ofstream*)status.fileoptr).open(filename.c_str(), ios_base::app); // Open for appending
            if (!*status.fileoptr)
            {
                delete status.fileoptr;
                status.fileoptr = status.optr;
                throw IfaceExecutionError("Unable to open file: " + filename);
            }
        }
    }
}
