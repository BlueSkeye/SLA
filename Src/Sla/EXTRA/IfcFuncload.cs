using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcFuncload : IfaceDecompCommand
    {
        /// \class IfcFuncload
        /// \brief Make a specific function current: `load function <functionname>`
        ///
        /// The name must be a fully qualified symbol with "::" separating namespaces.
        /// If the symbol represents a function, that function becomes \e current for
        /// the console. If there are bytes for the function, raw p-code and control-flow
        /// are calculated.
        public override void execute(TextReader s)
        {
            string funcname;
            Address offset;

            s >> funcname;

            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No image loaded");

            string basename;
            Scope* funcscope = dcp.conf.symboltab.resolveScopeFromSymbolName(funcname, "::", basename, (Scope*)0);
            if (funcscope == (Scope*)0)
                throw IfaceExecutionError("Bad namespace: " + funcname);
            dcp.fd = funcscope.queryFunction(basename); // Is function already in database
            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("Unknown function name: " + funcname);

            if (!dcp.fd.hasNoCode())
                dcp.followFlow(*status.optr, 0);
        }
    }
}
