using Sla.CORE;
using Sla.DECCORE;

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
            string name;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            Address pc;
            // Get specified varnode
            Address loc = Grammar.parse_varnode(s, out size, out pc, out uq, dcp.conf.types);
            Datatype ct = Grammar.parse_type(s, out name, dcp.conf);

            // Make sure varnodes are cleared
            dcp.conf.clearAnalysis(dcp.fd);

            Scope? scope = dcp.fd.getScopeLocal().discoverScope(loc, size, pc);
            if (scope == (Scope)null) // Variable does not have natural scope
                scope = dcp.fd.getScopeLocal();   // force it to be in function scope
            Symbol sym = scope.addSymbol(name, ct, loc, pc).getSymbol();
            scope.setAttribute(sym, Varnode.varnode_flags.typelock);
            sym.setIsolated(true);
            if (name.Length > 0)
                scope.setAttribute(sym, Varnode.varnode_flags.namelock);
            status.fileoptr.WriteLine(
                $"Successfully added {sym.getName()} to scope {scope.getFullName()}");
        }
    }
}
