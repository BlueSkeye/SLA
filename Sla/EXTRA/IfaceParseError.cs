using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief An exception describing a parsing error in a command line
    ///
    /// Thrown when attempting to parse a command line.  Options are missing or are in
    /// the wrong form etc.
    internal class IfaceParseError : IfaceError
    {
        internal IfaceParseError(string s)
            : base(s)
        {
        }
    }
}
