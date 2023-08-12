using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief A generic recoverable error
    ///
    /// This error is the most basic form of recoverable error,
    /// meaning there is some problem that the user did not take
    /// into account.
    internal class RecovError : LowlevelError
    {
        /// Initialize the error with an explanatory string
        internal RecovError(string s)
            : base(s)
        {
        }
    }
}
