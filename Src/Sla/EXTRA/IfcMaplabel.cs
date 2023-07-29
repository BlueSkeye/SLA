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
    internal class IfcMaplabel : IfaceDecompCommand
    {
        /// \class IfcMaplabel
        /// \brief Create a code label: `map label <name> <address>`
        ///
        /// Label a specific code address.  This creates a LabSymbol which is usually
        /// an internal control-flow target.  The symbol is local to the \e current function
        /// if it exists, otherwise the symbol is added to the global scope.
        public override void execute(TextReader s)
        {
            string name;
            s >> name;
            if (name.size() == 0)
                throw IfaceParseError("Need label name and address");
            int4 size;
            Address addr = parse_machaddr(s, size, *dcp.conf.types); // Read address

            Scope* scope;
            if (dcp.fd != (Funcdata*)0)
                scope = dcp.fd.getScopeLocal();
            else
                scope = dcp.conf.symboltab.getGlobalScope();

            Symbol* sym = scope.addCodeLabel(addr, name);
            scope.setAttribute(sym, Varnode::namelock | Varnode::typelock);
        }
    }
}
