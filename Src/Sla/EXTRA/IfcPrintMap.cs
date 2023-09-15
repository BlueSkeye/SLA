using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcPrintMap : IfaceDecompCommand
    {
        /// \class IfcPrintMap
        /// \brief Print info about a scope/namespace: `print map <name>`
        ///
        /// Prints information about the discoverable memory ranges for the scope,
        /// and prints a description of every symbol in the scope.
        public override void execute(TextReader s)
        {
            Scope? scope;
            string name = s.ReadString();

            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image");
            if (name.Length != 0 || dcp.fd == (Funcdata)null) {
                // Add fake variable name
                string? fullname;
                scope = dcp.conf.symboltab.resolveScopeFromSymbolName(name + "::a", "::",
                    out fullname, (Scope)null);
            }
            else {
                scope = dcp.fd.getScopeLocal();
            }

            if (scope == (Scope)null)
                throw new IfaceExecutionError("No map named: " + name);

            status.fileoptr.WriteLine(scope.getFullName());
            scope.printBounds(status.fileoptr);
            scope.printEntries(status.fileoptr);
        }
    }
}
