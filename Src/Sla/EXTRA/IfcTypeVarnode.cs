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
    internal class IfcTypeVarnode : IfaceDecompCommand
    {
        /// \class IfcTypeVarnode
        /// \brief Attach a typed symbol to a specific Varnode: `type varnode <varnode> <typedeclaration>`
        ///
        /// A new local symbol is created for the \e current function, and
        /// is attached to the specified Varnode. The \e current function must be decompiled
        /// again to see the effects.  The new symbol is \e type-locked with the data-type specified
        /// in the type declaration.  If a name is specified in the declaration, the symbol
        /// is \e name-locked as well.
        public override void execute(TextReader s)
        {
            int size;
            uint uq;
            Datatype* ct;
            string name;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            Address pc;
            Address loc = new Address(parse_varnode(s, size, pc, uq,* dcp.conf.types)); // Get specified varnode
            ct = parse_type(s, name, dcp.conf);

            dcp.conf.clearAnalysis(dcp.fd); // Make sure varnodes are cleared

            Scope scope = dcp.fd.getScopeLocal().discoverScope(loc, size, pc);
            if (scope == (Scope)null) // Variable does not have natural scope
                scope = dcp.fd.getScopeLocal();   // force it to be in function scope
            Symbol sym = scope.addSymbol(name, ct, loc, pc).getSymbol();
            scope.setAttribute(sym, Varnode.varnode_flags.typelock);
            sym.setIsolated(true);
            if (name.size() > 0)
                scope.setAttribute(sym, Varnode.varnode_flags.namelock);

            status.fileoptr << "Successfully added " << sym.getName();
            status.fileoptr << " to scope " << scope.getFullName() << endl;
        }
    }
}
