using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Exception indicating data was not available
    ///
    /// This exception is thrown when a request for load image
    /// data cannot be met, usually because the requested address
    /// range is not in the image.
    internal class DataUnavailError : LowlevelError
    {
        /// Instantiate with an explanatory string
        internal DataUnavailError(string s)
            : base(s)
        {
        }
    }
}
