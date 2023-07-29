using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcReadonly : IfaceDecompCommand
    {
        /// \class IfcReadonly
        /// \brief Mark a memory range as read-only: `readonly <address+size>`
        ///
        /// The memory range provided on the command-line is marked as \e read-only, which
        /// allows the decompiler to propagate values pulled from the LoadImage for the range
        /// as constants.
        public override void execute(TextReader s)
        {
            int size = 0;
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");
            Address addr = parse_machaddr(s, size, *dcp.conf.types); // Read required address

            if (size == 0)
                throw IfaceExecutionError("Must specify a size");
            Range range(addr.getSpace(), addr.getOffset(), addr.getOffset() +(size - 1));
            dcp.conf.symboltab.setPropertyRange(Varnode::readonly, range);

            *status.optr << "Successfully marked range as readonly" << endl;
        }
    }
}
