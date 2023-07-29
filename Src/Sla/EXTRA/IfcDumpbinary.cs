using Sla.CORE;
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
            int4 size;
            uint1* buffer;
            Address offset = parse_machaddr(s, size, *dcp->conf->types);
            string filename;

            s >> ws;
            if (s.eof())
                throw IfaceParseError("Missing file name for binary dump");
            s >> filename;
            ofstream os;
            os.open(filename.c_str());
            if (!os)
                throw IfaceExecutionError("Unable to open file " + filename);

            buffer = dcp->conf->loader->load(size, offset);
            os.write((char*)buffer,size);
            delete[] buffer;
            os.close();
        }
    }
}
