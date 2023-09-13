using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcRemove : IfaceDecompCommand
    {
        /// \class IfcRemove
        /// \brief Remove a symbol by name: `remove <symbolname>`
        ///
        /// The symbol is searched for starting in the current function's scope.
        /// The resulting symbol is removed completely from the symbol table.
        public override void execute(TextReader s)
        {
            s.ReadSpaces();
            string name = s.ReadString();
            if (name.Length == 0)
                throw new IfaceParseError("Missing symbol name");

            List<Symbol> symList = new List<Symbol>();
            dcp.readSymbol(name, symList);

            if (symList.empty())
                throw new IfaceExecutionError($"No symbol named: {name}");
            if (symList.size() > 1)
                throw new IfaceExecutionError($"More than one symbol named: {name}");
            symList[0].getScope().removeSymbol(symList[0]);
        }
    }
}
