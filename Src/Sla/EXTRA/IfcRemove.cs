using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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
            string name;

            s >> ws >> name;
            if (name.size() == 0)
                throw IfaceParseError("Missing symbol name");

            List<Symbol*> symList;
            dcp.readSymbol(name, symList);

            if (symList.empty())
                throw IfaceExecutionError("No symbol named: " + name);
            if (symList.size() > 1)
                throw IfaceExecutionError("More than one symbol named: " + name);
            symList[0].getScope().removeSymbol(symList[0]);
        }
    }
}
