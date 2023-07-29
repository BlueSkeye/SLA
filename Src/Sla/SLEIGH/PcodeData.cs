using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief Data for building one p-code instruction
    ///
    /// Raw data used by the emitter to produce a single PcodeOp
    internal struct PcodeData
    {
        internal OpCode opc;         ///< The op code
        internal VarnodeData outvar;            ///< Output Varnode data (or null)
        internal VarnodeData invar;     ///< Array of input Varnode data
        internal int isize;			///< Number of input Varnodes
    }
}
