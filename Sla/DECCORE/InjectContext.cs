using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Context needed to emit a p-code injection as a full set of p-code operations
    ///
    /// P-code injection works by passing a pre-built template of p-code operations (ConstructTpl)
    /// to an emitter (PcodeEmit), which makes the final resolution SLEIGH concepts like \e inst_next to
    /// concrete Varnodes. This class contains the context dependent data to resolve:
    ///   - inst_start  -- the address where the injection occurs
    ///   - inst_next   -- the address of the instruction following (the instruction being injected)
    ///   - inst_next2  -- the address of the instruction after the next instruction (Not Supported)
    ///   - inst_dest   -- Original destination of CALL being injected
    ///   - inst_ref    -- Target of reference on injected instruction
    ///   - \<input>     -- Input Varnode of the injection referenced by name
    ///   - \<output>    -- Output Varnode of the injection referenced by name
    internal abstract class InjectContext
    {
        /// Architecture associated with the injection
        public Architecture glb;
        /// Address of instruction causing inject
        public Address baseaddr;
        /// Address of following instruction
        public Address nextaddr;
        /// If the instruction being injected is a call, this is the address being called
        public Address calladdr;
        /// Storage location for input parameters
        public List<VarnodeData> inputlist;
        /// Storage location for output
        public List<VarnodeData> output;
        
        ~InjectContext()
        {
        }

        /// Release resources (from last injection)
        public virtual void clear()
        {
            inputlist.clear();
            output.clear();
        }

        /// \brief Encode \b this context to a stream as a \<context> element
        ///
        /// \param encoder is the stream encoder
        public abstract void encode(Encoder encoder);
    }
}
