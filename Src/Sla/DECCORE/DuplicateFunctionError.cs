using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    /// \brief Exception thrown when a function is added more than once to the database
    /// Stores off the address of the function, so a handler can recover from the exception
    /// and pick up the original symbol.
    internal class DuplicateFunctionError : RecovError
    {
        // Address of function causing the error
        private Address address;
        // Name of the function
        private string functionName;

        internal DuplicateFunctionError(Address addr, string nm)
            : base("Duplicate Function")
        {
            address = addr;
            functionName = nm;
        }
    }
}
