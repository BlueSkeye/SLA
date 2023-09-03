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
            string token;
            int size;
            uint uq;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            Address pc;
            Address loc = new Address(parse_varnode(s, size, pc, uq,* dcp.conf.types)); // Get specified varnode

            s >> ws >> token;       // Get the new name of the varnode
            if (token.size() == 0)
                throw new IfaceParseError("Must specify name");

            Datatype ct = dcp.conf.types.getBase(size, type_metatype.TYPE_UNKNOWN);

            dcp.conf.clearAnalysis(dcp.fd); // Make sure varnodes are cleared

            Scope scope = dcp.fd.getScopeLocal().discoverScope(loc, size, pc);
            if (scope == (Scope)null) // Variable does not have natural scope
                scope = dcp.fd.getScopeLocal();   // force it to be in function scope
            Symbol sym = scope.addSymbol(token, ct, loc, pc).getSymbol();
            scope.setAttribute(sym, Varnode.varnode_flags.namelock);

            status.fileoptr << "Successfully added " << token;
            status.fileoptr << " to scope " << scope.getFullName() << endl;
        }
    }
}
