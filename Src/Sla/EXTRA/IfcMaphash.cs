using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMaphash : IfaceDecompCommand
    {
        /// \class IfcMaphash
        /// \brief Add a dynamic symbol to the current function: `map hash <address> <hash> <typedeclaration>`
        ///
        /// The command only creates local variables for the current function.
        /// The name and data-type are taken from a C syntax type declaration.  The symbol is
        /// not associated with a particular storage address but with a specific Varnode in the data-flow,
        /// specified by a code address and hash of the local data-flow structure.
        public override void execute(TextReader s)
        {
            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function loaded");
            Datatype* ct;
            string name;
            uint8 hash;
            int4 size;
            Address addr = parse_machaddr(s, size, *dcp->conf->types); // Read pc address of hash

            s >> hex >> hash;       // Parse the hash value
            s >> ws;
            ct = parse_type(s, name, dcp->conf); // Parse the required type and name

            Symbol* sym = dcp->fd->getScopeLocal()->addDynamicSymbol(name, ct, addr, hash);
            sym->getScope()->setAttribute(sym, Varnode::namelock | Varnode::typelock);
        }
    }
}
