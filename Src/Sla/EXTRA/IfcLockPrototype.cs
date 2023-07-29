using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcLockPrototype : IfaceDecompCommand
    {
        /// \class IfcLockPrototype
        /// \brief Lock in the \e current function's prototype: `prototype lock`
        ///
        /// Lock in the existing formal parameter names and data-types for any future
        /// decompilation.  Both input parameters and the return value are locked.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            dcp.fd.getFuncProto().setInputLock(true);
            dcp.fd.getFuncProto().setOutputLock(true);
        }
    }
}
