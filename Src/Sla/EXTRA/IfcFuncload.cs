using Sla.CORE;
using Sla.DECCORE;

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
            string funcname = s.ReadString();
            Address offset;

            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No image loaded");

            string basename;
            Scope? funcscope = dcp.conf.symboltab.resolveScopeFromSymbolName(funcname, "::", basename, (Scope)null);
            if (funcscope == (Scope)null)
                throw new IfaceExecutionError("Bad namespace: " + funcname);
            dcp.fd = funcscope.queryFunction(basename); // Is function already in database
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("Unknown function name: " + funcname);

            if (!dcp.fd.hasNoCode())
                dcp.followFlow(status.optr, 0);
        }
    }
}
