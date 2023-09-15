using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcNameVarnode : IfaceDecompCommand
    {
        /// \class IfcNameVarnode
        /// \brief Attach a named symbol to a specific Varnode: `name varnode <varnode> <name>`
        ///
        /// A new local symbol is created for the \e current function, and
        /// is attached to the specified Varnode. The \e current function must be decompiled
        /// again to see the effects.  The new symbol is \e name-locked with the specified
        /// name, but the data-type of the symbol is allowed to float.
        public override void execute(TextReader s)
        {
            int size;
            uint uq;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            Address pc;
            // Get specified varnode
            Address loc = Grammar.parse_varnode(s, out size, out pc, out uq, dcp.conf.types);

            s.ReadSpaces();
            // Get the new name of the varnode
            string token = s.ReadString();
            if (token.Length == 0)
                throw new IfaceParseError("Must specify name");

            Datatype ct = dcp.conf.types.getBase(size, type_metatype.TYPE_UNKNOWN);
            dcp.conf.clearAnalysis(dcp.fd); // Make sure varnodes are cleared
            Scope? scope = dcp.fd.getScopeLocal().discoverScope(loc, size, pc);
            if (scope == (Scope)null)
                // Variable does not have natural scope
                // force it to be in function scope
                scope = dcp.fd.getScopeLocal();
            Symbol sym = scope.addSymbol(token, ct, loc, pc).getSymbol();
            scope.setAttribute(sym, Varnode.varnode_flags.namelock);

            status.fileoptr.WriteLine(
                $"Successfully added {token} to scope {scope.getFullName()}");
        }
    }
}
