using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A record indicating a function symbol
    ///
    /// This is a lightweight object holding the Address and name of a function
    internal struct LoadImageFunc
    {
        /// Start of function
        internal Address address;
        /// Name of function
        internal string name;
    }
}
