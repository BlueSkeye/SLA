using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMapexternalref : IfaceDecompCommand
    {
        /// \class IfcMapexternalref
        /// \brief Create an external ref symbol `map externalref <address> <refaddress> [<name>]`
        ///
        /// Creates a symbol for a function pointer and associates a specific address as
        /// a value for that symbol.  The first address specified is the address of the symbol,
        /// The second address is the address referred to by the pointer.  Indirect calls
        /// through the function pointer will be converted to direct calls to the referred address.
        /// A symbol name can be provided, otherwise a default one is generated.
        public override void execute(TextReader s)
        {
            int4 size1, size2;
            Address addr1 = parse_machaddr(s, size1, *dcp->conf->types); // Read externalref address
            Address addr2 = parse_machaddr(s, size2, *dcp->conf->types); // Read referred to address
            string name;

            s >> name;          // Read optional name

            dcp->conf->symboltab->getGlobalScope()->addExternalRef(addr1, addr2, name);
        }
    }
}
