using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcRetype : IfaceDecompCommand
    {
        /// \class IfcRetype
        /// \brief Change the data-type of a symbol: `retype <symbolname> <typedeclaration>`
        /// The symbol is searched for by name starting in the current function's scope.
        /// If the type declaration includes a new name for the variable, the
        /// variable is renamed as well.
        public override void execute(TextReader s)
        {

            s.ReadSpaces();
            string name = s.ReadString();
            if (string.IsNullOrEmpty(name))
                throw new IfaceParseError("Must specify name of symbol");
            string newname;
            Datatype ct = Grammar.parse_type(s, out newname, dcp.conf);

            List<Symbol> symList = new List<Symbol>();
            dcp.readSymbol(name, symList);

            if (symList.empty())
                throw new IfaceExecutionError("No symbol named: " + name);
            if (symList.size() > 1)
                throw new IfaceExecutionError("More than one symbol named : " + name);
            Symbol sym = symList[0];

            if (sym.getCategory() == Symbol.SymbolCategory.function_parameter)
                dcp.fd.getFuncProto().setInputLock(true);
            sym.getScope().retypeSymbol(sym, ct);
            sym.getScope().setAttribute(sym, Varnode.varnode_flags.typelock);
            if ((newname.Length != 0) && (newname != name)) {
                sym.getScope().renameSymbol(sym, newname);
                sym.getScope().setAttribute(sym, Varnode.varnode_flags.namelock);
            }
        }
    }
}
