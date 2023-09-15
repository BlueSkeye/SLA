using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMapfunction : IfaceDecompCommand
    {
        /// \class IfcMapfunction
        /// \brief Create a new function: `map function <address> [<functionname>] [nocode]`
        ///
        /// Create a new function symbol at the provided address.
        /// A symbol name can be provided, otherwise a default name is selected.
        /// The new function becomes \e current for the console.
        /// The provided address gives the entry point for the function.  Unless the final keyword
        /// "nocode" is provided, the underlying bytes in the load image are used for any
        /// future disassembly or decompilation.
        public override void execute(TextReader s)
        {
            string name;
            int size;
            if ((dcp.conf == (Architecture)null) || (dcp.conf.loader == (LoadImage)null))
                throw new IfaceExecutionError("No binary loaded");
            // Read required address;
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types);

            name = s.ReadString();          // Read optional name
            if (name.empty())
                // Pick default name if necessary
                dcp.conf.nameFunction(addr, out name);
            string basename;
            Scope scope = dcp.conf.symboltab.findCreateScopeFromSymbolName(name, "::"
                out basename, (Scope)null);
            dcp.fd = scope.addFunction(addr, name).getFunction();

            s.ReadSpaces();
            string nocode = s.ReadString();
            if (nocode == "nocode") {
                dcp.fd.setNoCode(true);
            }
        }
    }
}
