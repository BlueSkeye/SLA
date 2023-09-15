using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcSource : IfaceDecompCommand
    {
        /// \class IfcSource
        /// \brief Execute a command script : `source <filename>`
        ///
        /// A file is opened as a new streaming source of command-lines.
        /// The stream is pushed onto the stack for the console.
        public override void execute(TextReader s)
        {
            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("filename parameter required for source");
            string filename = s.ReadString();
            status.pushScript(filename, filename + "> ");
        }
    }
}
