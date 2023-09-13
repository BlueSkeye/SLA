using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcDumpbinary : IfaceDecompCommand
    {
        /// \class IfcDumpbinary
        /// \brief Dump a memory to file: `binary <address+size> <filename>`
        ///
        /// Raw bytes from the specified memory region in the load image are written
        /// to a file.
        public override void execute(TextReader s)
        {
            int size;
            byte[] buffer;
            Address offset = Grammar.parse_machaddr(s, out size, dcp.conf.types);

            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("Missing file name for binary dump");
            string filename = s.ReadString();
            TextWriter os;
            try { os = new StreamWriter(File.OpenWrite(filename)); }
            catch {
                throw new IfaceExecutionError($"Unable to open file {filename}");
            }
            try {
                buffer = dcp.conf.loader.load(size, offset);
                os.Write(buffer, size);
                // delete[] buffer;
            }
            finally { os.Close(); }
        }
    }
}
