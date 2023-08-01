using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// This exception is thrown when emulation evaluation of an operator fails for some reason.
    /// This can be thrown for either forward or reverse emulation
    internal class EvaluationError : LowlevelError
    {
        /// Initialize the error with an explanatory string
        internal EvaluationError(string s)
            : base(s)
        {
        }
    }
}
