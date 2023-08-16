using Sla.CORE;

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
