using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcUnlockPrototype : IfaceDecompCommand
    {
        /// \class IfcUnlockPrototype
        /// \brief Unlock the \e current function's prototype: `prototype unlock`
        ///
        /// Unlock all input parameters and the return value, so future decompilation
        /// is not constrained with their data-type or name.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            dcp.fd.getFuncProto().setInputLock(false);
            dcp.fd.getFuncProto().setOutputLock(false);
        }
    }
}
