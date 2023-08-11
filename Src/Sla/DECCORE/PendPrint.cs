using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Helper class for sending cancelable print commands to an ExitXml
    ///
    /// The PendPrint is issued as a placeholder for commands to the emitter using its
    /// setPendingPrint() method.  The callback() method is overridden to tailor the exact
    /// sequence of print commands.  The print commands will be executed prior to the next
    /// tagLine() call to the emitter, unless the PendPrint is cancelled.
    internal abstract class PendPrint
    {
        ~PendPrint()
        {
        }

        /// Callback that executes the actual print commands
        public abstract void callback(Emit emit);
    }
}
