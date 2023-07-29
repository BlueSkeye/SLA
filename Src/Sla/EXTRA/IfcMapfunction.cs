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
            int4 size;
            if ((dcp->conf == (Architecture*)0) || (dcp->conf->loader == (LoadImage*)0))
                throw IfaceExecutionError("No binary loaded");

            Address addr = parse_machaddr(s, size, *dcp->conf->types); // Read required address;

            s >> name;          // Read optional name
            if (name.empty())
                dcp->conf->nameFunction(addr, name); // Pick default name if necessary
            string basename;
            Scope* scope = dcp->conf->symboltab->findCreateScopeFromSymbolName(name, "::", basename, (Scope*)0);
            dcp->fd = scope->addFunction(addr, name)->getFunction();

            string nocode;
            s >> ws >> nocode;
            if (nocode == "nocode")
                dcp->fd->setNoCode(true);
        }
    }
}
