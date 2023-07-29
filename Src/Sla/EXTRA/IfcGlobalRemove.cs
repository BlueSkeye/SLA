using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcGlobalRemove : IfaceDecompCommand
    {
        /// \class IfcGlobalRemove
        /// \brief Remove a memory range from discoverable global variables: `global remove <address+size>`
        ///
        /// The will no longer treat Varnodes stored in the memory range as persistent global
        /// variables.  The will be treated as local or temporary storage.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No image loaded");

            int4 size;
            Address addr = parse_machaddr(s, size, *dcp.conf.types);
            uintb first = addr.getOffset();
            uintb last = first + (size - 1);

            Scope* scope = dcp.conf.symboltab.getGlobalScope();
            dcp.conf.symboltab.removeRange(scope, addr.getSpace(), first, last);
        }
    }
}
