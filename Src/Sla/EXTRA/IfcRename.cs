using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcRename : IfaceDecompCommand
    {
        /// \class IfcRename
        /// \brief Rename a variable: `rename <oldname> <newname>`
        ///
        /// Change the name of a symbol.  The provided name is searched for starting
        /// in the scope of the current function.
        public override void execute(TextReader s)
        {
            s.ReadSpaces();
            string oldname = s.ReadString();
            s.ReadSpaces();
            string newname  = s.ReadString();
            s.ReadSpaces();
            if (oldname.Length == 0)
                throw new IfaceParseError("Missing old symbol name");
            if (newname.Length == 0)
                throw new IfaceParseError("Missing new name");

            Symbol sym;
            List<Symbol> symList = new List<Symbol>();
            dcp.readSymbol(oldname, symList);

            if (symList.empty())
                throw new IfaceExecutionError("No symbol named: " + oldname);
            if (symList.size() == 1)
                sym = symList[0];
            else
                throw new IfaceExecutionError("More than one symbol named: " + oldname);

            if (sym.getCategory() == Symbol.SymbolCategory.function_parameter)
                dcp.fd.getFuncProto().setInputLock(true);
            sym.getScope().renameSymbol(sym, newname);
            sym.getScope().setAttribute(sym, Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock);
        }
    }
}
