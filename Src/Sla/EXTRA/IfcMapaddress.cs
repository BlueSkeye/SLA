using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMapaddress : IfaceDecompCommand
    {
        /// \class IfcMapaddress
        /// \brief Map a new symbol into the program: `map address <address> <typedeclaration>`
        ///
        /// Create a new variable in the current scope
        /// \code
        ///    map address r0x1000 int globalvar
        /// \endcode
        /// The symbol specified in the type declaration can qualify the namespace using the "::"
        /// specifier.  If there is a current function, the variable is local to the function.
        /// Otherwise the symbol is created relative to the global scope.
        public override void execute(TextReader s)
        {
            Datatype* ct;
            string name;
            int size;
            Address addr = parse_machaddr(s, size, *dcp.conf.types); // Read required address;

            s >> ws;
            ct = parse_type(s, name, dcp.conf); // Parse the required type
            if (dcp.fd != (Funcdata*)0)
            {
                Symbol* sym;
                sym = dcp.fd.getScopeLocal().addSymbol(name, ct, addr, Address()).getSymbol();
                sym.getScope().setAttribute(sym, Varnode::namelock | Varnode::typelock);
            }
            else
            {
                Symbol* sym;
                uint flags = Varnode::namelock | Varnode::typelock;
                flags |= dcp.conf.symboltab.getProperty(addr); // Inherit existing properties
                string basename;
                Scope* scope = dcp.conf.symboltab.findCreateScopeFromSymbolName(name, "::", basename, (Scope*)0);
                sym = scope.addSymbol(basename, ct, addr, Address()).getSymbol();
                sym.getScope().setAttribute(sym, flags);
                if (scope.getParent() != (Scope*)0)
                {       // If this is a global namespace scope
                    SymbolEntry* e = sym.getFirstWholeMap();       // Adjust range
                    dcp.conf.symboltab.addRange(scope, e.getAddr().getSpace(), e.getFirst(), e.getLast());
                }
            }

        }
    }
}
