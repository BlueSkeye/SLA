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
            Datatype ct;
            string name;
            int size;
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types); // Read required address;

            s.ReadSpaces();
            ct = parse_type(s, name, dcp.conf); // Parse the required type
            if (dcp.fd != (Funcdata)null) {
                Symbol sym = dcp.fd.getScopeLocal().addSymbol(name, ct, addr, new Address()).getSymbol();
                sym.getScope().setAttribute(sym, Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock);
            }
            else {
                Symbol sym;
                Varnode.varnode_flags flags =
                    Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock;
                flags |= dcp.conf.symboltab.getProperty(addr); // Inherit existing properties
                string basename;
                Scope scope = dcp.conf.symboltab.findCreateScopeFromSymbolName(name, "::", basename, (Scope)null);
                sym = scope.addSymbol(basename, ct, addr, new Address()).getSymbol();
                sym.getScope().setAttribute(sym, flags);
                if (scope.getParent() != (Scope)null) {
                    // If this is a global namespace scope
                    SymbolEntry e = sym.getFirstWholeMap();       // Adjust range
                    dcp.conf.symboltab.addRange(scope, e.getAddr().getSpace(), e.getFirst(), e.getLast());
                }
            }

        }
    }
}
