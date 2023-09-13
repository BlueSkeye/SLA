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
    internal class IfcDump : IfaceDecompCommand
    {
        /// \class IfcDump
        /// \brief Display bytes in the load image: `dump <address+size>`
        ///
        /// The command does a hex listing of the specific memory region.
        public override void execute(TextReader s)
        {
            int size;
            Address offset = Grammar.parse_machaddr(s, out size, dcp.conf.types);

            byte[] buffer = dcp.conf.loader.load(size, offset);
            print_data(status.fileoptr, buffer, size, offset);
            // delete[] buffer;
        }
    }
}
