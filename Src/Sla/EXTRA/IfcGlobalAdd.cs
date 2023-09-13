using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcGlobalAdd : IfaceDecompCommand
    {
        /// \class IfcGlobalAdd
        /// \brief Add a memory range as discoverable global variables: `global add <address+size>`
        ///
        /// The decompiler will treat Varnodes stored in the new memory range as persistent
        /// global variables.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null) {
                throw new IfaceExecutionError("No image loaded");
            }

            int size;
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types);
            ulong first = addr.getOffset();
            ulong last = first + (uint)(size - 1);
            Scope scope = dcp.conf.symboltab.getGlobalScope() ?? throw new ApplicationException();
            dcp.conf.symboltab.addRange(scope, addr.getSpace(), first, last);
        }
    }
}
