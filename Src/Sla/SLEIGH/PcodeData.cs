using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief Data for building one p-code instruction
    /// Raw data used by the emitter to produce a single PcodeOp
    internal struct PcodeData
    {
        /// The op code
        internal OpCode opc;
        /// Output Varnode data (or null)
        internal VarnodeData outvar;
        /// Array of input Varnode data
        internal VarnodeData[] invar;
        /// Number of input Varnodes
        internal int isize;
    }
}
