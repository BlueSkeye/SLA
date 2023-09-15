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
    internal class IfcIsolate : IfaceDecompCommand
    {
        /// \class IfcIsolate
        /// \brief Mark a symbol as isolated from speculative merging: `isolate <name>`
        public override void execute(TextReader s)
        {
            s.ReadSpaces();
            string symbolName = s.ReadString();
            if (symbolName.Length == 0)
                throw new IfaceParseError("Missing symbol name");

            List<Symbol> symList = new List<Symbol>();
            dcp.readSymbol(symbolName, symList);
            if (symList.empty())
                throw new IfaceExecutionError("No symbol named: " + symbolName);
            if (symList.size() != 1)
                throw new IfaceExecutionError("More than one symbol named: " + symbolName);
            Symbol sym = symList[0];
            sym.setIsolated(true);
        }
    }
}
