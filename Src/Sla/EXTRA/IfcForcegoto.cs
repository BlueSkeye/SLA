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
            int discard;

            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            s >> ws;
            Address target(parse_machaddr(s, discard,* dcp.conf.types));
            s >> ws;
            Address dest(parse_machaddr(s, discard,* dcp.conf.types));
            dcp.fd.getOverride().insertForceGoto(target, dest);
        }
    }
}
