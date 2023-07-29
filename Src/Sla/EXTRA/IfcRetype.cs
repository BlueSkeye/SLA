using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcRetype
    {
        /// \class IfcRetype
        /// \brief Change the data-type of a symbol: `retype <symbolname> <typedeclaration>`
        ///
        /// The symbol is searched for by name starting in the current function's scope.
        /// If the type declaration includes a new name for the variable, the
        /// variable is renamed as well.
        public override void execute(TextReader s)
        {
            Datatype* ct;
            string name, newname;

            s >> ws >> name;
            if (name.size() == 0)
                throw new IfaceParseError("Must specify name of symbol");
            ct = parse_type(s, newname, dcp.conf);

            Symbol* sym;
            List<Symbol*> symList;
            dcp.readSymbol(name, symList);

            if (symList.empty())
                throw new IfaceExecutionError("No symbol named: " + name);
            if (symList.size() > 1)
                throw new IfaceExecutionError("More than one symbol named : " + name);
            else
                sym = symList[0];

            if (sym.getCategory() == Symbol::function_parameter)
                dcp.fd.getFuncProto().setInputLock(true);
            sym.getScope().retypeSymbol(sym, ct);
            sym.getScope().setAttribute(sym, Varnode::typelock);
            if ((newname.size() != 0) && (newname != name))
            {
                sym.getScope().renameSymbol(sym, newname);
                sym.getScope().setAttribute(sym, Varnode::namelock);
            }
        }
    }
}
