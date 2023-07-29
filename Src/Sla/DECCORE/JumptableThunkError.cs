using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Exception thrown for a thunk mechanism that looks like a jump-table
    internal class JumptableThunkError : LowlevelError
    {
        ///< Construct with an explanatory string
        internal JumptableThunkError(string s)
            : base(s)
        {
        }
    }
}
