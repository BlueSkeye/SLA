using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief An exception throw during the execution of a command
    ///
    /// Processing of a specific command has started but has reached an error state
    internal class IfaceExecutionError : IfaceError
    {
        internal IfaceExecutionError(string s)
            : base(s)
        {
        }
    }
}
