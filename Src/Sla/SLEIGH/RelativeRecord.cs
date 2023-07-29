using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief Class for describing a relative p-code branch destination
    ///
    /// An intra-instruction p-code branch takes a \e relative operand.
    /// The actual value produced during p-code generation is calculated at
    /// the last second using \b this. It stores the index of the BRANCH
    /// instruction and a reference to its destination operand. This initially
    /// holds a reference to a destination \e label symbol, but is later updated
    /// with the final relative value.
    internal struct RelativeRecord
    {
        internal VarnodeData dataptr;       ///< Varnode indicating relative offset
        internal ulong calling_index;		///< Index of instruction containing relative offset
    }
}
