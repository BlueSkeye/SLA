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
    internal class IfcForcegoto : IfaceDecompCommand
    {
        /// \class IfcForcegoto
        /// \brief Force a branch to be an unstructured \b goto: `force goto <branchaddr> <targetaddr>`
        ///
        /// Create an override that forces the decompiler to treat the specified branch
        /// as unstructured. The branch will be modeled as a \b goto statement.
        /// The branch is specified by first providing the address of the branching instruction,
        /// then the destination address.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null) {
                throw new IfaceExecutionError("No function selected");
            }
            s.ReadSpaces();
            int discard;
            Address target = Grammar.parse_machaddr(s, out discard, dcp.conf.types);
            s.ReadSpaces();
            Address dest = Grammar.parse_machaddr(s, out discard, dcp.conf.types);
            dcp.fd.getOverride().insertForceGoto(target, dest);
        }
    }
}
