using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief P-code emitter that dumps its raw Varnodes and PcodeOps to an in memory cache
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
        private uint uniq;

        /// Clone and cache a raw VarnodeData
        /// Create an internal copy of the VarnodeData and cache it.
        /// \param var is the incoming VarnodeData being dumped
        /// \return the cloned VarnodeData
        private VarnodeData createVarnode(VarnodeData var)
        {
            VarnodeData res = new VarnodeData();
            res = var;
            varcache.Add(res);
            return res;
        }

        /// Constructor
        public PcodeEmitCache(List<PcodeOpRaw> ocache, List<VarnodeData> vcache,
            List<OpBehavior> @in, ulong uniqReserve)
        {
            opcache = ocache;
            varcache = vcache;
            inst = @in;
            uniq = (uint)uniqReserve;
        }

        public override void dump(Address addr, OpCode opc, VarnodeData? outvar,
            VarnodeData[] vars, int isize)
        {
            PcodeOpRaw op = new PcodeOpRaw();
            op.setSeqNum(addr, uniq);
            opcache.Add(op);
            op.setBehavior(inst[(int)opc]);
            uniq += 1;
            if (outvar != (VarnodeData)null) {
                VarnodeData outvn = createVarnode(outvar);
                op.setOutput(outvn);
            }
            for (int i = 0; i < isize; ++i) {
                VarnodeData invn = createVarnode(vars + i);
                op.addInput(invn);
            }
        }
    }
}
