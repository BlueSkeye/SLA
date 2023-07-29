using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief P-code emitter that dumps its raw Varnodes and PcodeOps to an in memory cache
    ///
    /// This is used for emulation when full Varnode and PcodeOp objects aren't needed
    internal class PcodeEmitCache : PcodeEmit
    {
        /// The cache of current p-code ops
        private List<PcodeOpRaw> opcache;
        /// The cache of current varnodes
        private List<VarnodeData> varcache;
        /// Array of behaviors for translating OpCode
        private List<OpBehavior> inst;
        /// Starting offset for defining temporaries in \e unique space
        private uintm uniq;

        /// Clone and cache a raw VarnodeData
        private VarnodeData createVarnode(VarnodeData var);

        /// Constructor
        public PcodeEmitCache(List<PcodeOpRaw> ocache, List<VarnodeData> vcache,
            List<OpBehavior> @in, uintb uniqReserve);

        public override void dump(Address addr, OpCode opc, VarnodeData outvar, VarnodeData vars,
            int4 isize);
    }
}
