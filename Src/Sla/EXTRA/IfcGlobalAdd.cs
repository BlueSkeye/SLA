using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcGlobalAdd : IfaceDecompCommand
    {
        /// \class IfcGlobalAdd
        /// \brief Add a memory range as discoverable global variables: `global add <address+size>`
        ///
        /// The decompiler will treat Varnodes stored in the new memory range as persistent
        /// global variables.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No image loaded");

            int4 size;
            Address addr = parse_machaddr(s, size, *dcp.conf.types);
            uintb first = addr.getOffset();
            uintb last = first + (size - 1);

            Scope* scope = dcp.conf.symboltab.getGlobalScope();
            dcp.conf.symboltab.addRange(scope, addr.getSpace(), first, last);
        }
    }
}
