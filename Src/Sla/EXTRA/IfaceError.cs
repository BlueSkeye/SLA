using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief An exception specific to the command line interface
    internal class IfaceError : Exception
    {
        internal IfaceError(string s)
            :base(s)
        {
        }
    }
}
