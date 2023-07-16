using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Exception thrown when a function is added more than once to the database
    /// Stores off the address of the function, so a handler can recover from the exception
    /// and pick up the original symbol.
    internal class DuplicateFunctionError : RecovError
    {
        private Address address;        ///< Address of function causing the error
        private string functionName;        ///< Name of the function

        internal DuplicateFunctionError(Address addr, string nm)
            : base("Duplicate Function")
        {
            address = addr;
            functionName = nm;
        }
    }
}
