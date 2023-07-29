using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcDump : IfaceDecompCommand
    {
        /// \class IfcDump
        /// \brief Display bytes in the load image: `dump <address+size>`
        ///
        /// The command does a hex listing of the specific memory region.
        public override void execute(TextReader s)
        {
            int4 size;
            uint1* buffer;
            Address offset = parse_machaddr(s, size, *dcp->conf->types);

            buffer = dcp->conf->loader->load(size, offset);
            print_data(*status->fileoptr, buffer, size, offset);
            delete[] buffer;
        }
    }
}
