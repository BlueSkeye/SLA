using Sla.CORE;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A resolver for segmented architectures
    /// When the decompiler is attempting to resolve embedded constants as pointers,
    /// this class tries to recover segment info for near pointers by looking up
    /// tracked registers in context
    internal class SegmentedResolver : AddressResolver
    {
        ///< The architecture owning the segmented space
        private Architecture glb;
        ///< The address space being segmented
        private AddrSpace spc;
        ///< The segment operator
        private SegmentOp segop;

        /// Construct a segmented resolver
        /// \param g is the owning Architecture
        /// \param sp is the segmented space
        /// \param sop is the segment operator
        public SegmentedResolver(Architecture g, AddrSpace sp, SegmentOp sop)
        {
            glb = g;
            spc = sp;
            segop = sop;
        }

        public override Address resolve(ulong val, int sz, Address point, out ulong fullEncoding)
        {
            int innersz = segop.getInnerSize();
            if (sz >= 0 && sz <= innersz) {
                // If -sz- matches the inner size, consider the value a "near" pointer
                // In this case the address offset is not fully specified
                // we check if the rest is stored in a context variable
                // (as with near pointers)
                if (null != segop.getResolve().space) {
                    ulong @base = glb.context.getTrackedValue(segop.getResolve(), point);
                    fullEncoding = (@base << 8 * innersz)
                        + (val & Globals.calc_mask((uint)innersz));
                    List<ulong> seginput = new List<ulong>();
                    seginput.Add(@base);
                    seginput.Add(val);
                    val = segop.execute(seginput);
                    return new Address(spc, AddrSpace.addressToByte(val, spc.getWordSize()));
                }
            }
            else {
                // For anything else, consider it a "far" pointer
                fullEncoding = val;
                int outersz = segop.getBaseSize();
                ulong @base = (val >> 8 * innersz) & Globals.calc_mask((uint)outersz);
                val = val & Globals.calc_mask((uint)innersz);
                List<ulong> seginput = new List<ulong>();
                seginput.Add(@base);
                seginput.Add(val);
                val = segop.execute(seginput);
                return new Address(spc, AddrSpace.addressToByte(val, spc.getWordSize()));
            }
            // Return invalid address
            fullEncoding = 0;
            return new Address();
        }
    }
}
