using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Label for describing extent of address range that has been heritaged
    internal struct SizePass
    {
        /// Size of the range (in bytes)
        internal int4 size;
        /// Pass when the range was heritaged
        internal int4 pass;
    }
}
