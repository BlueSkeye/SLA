using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A user defined operation that is injected with other p-code
    ///
    /// The system can configure user defined p-code ops as a hook point within the
    /// control-flow where other p-code is injected during analysis. This class maps
    /// the raw CALLOTHER p-code op, via its constant id, to its injection object.
    /// The injection object is also referenced by an id and is managed by PcodeInjectLibrary.
    internal class InjectedUserOp : UserPcodeOp
    {
        private uint4 injectid;         ///< The id of the injection object (to which this op maps)
        
        public InjectedUserOp(Architecture g, string nm,int4 ind, int4 injid)
            : base(g, nm, ind)
        {
            injectid = injid;
        }

        /// Get the id of the injection object
        public uint4 getInjectId() => injectid;
    
        public override void decode(Decoder decoder)
        {
            injectid = glb.pcodeinjectlib.decodeInject("userop", "", InjectPayload::CALLOTHERFIXUP_TYPE, decoder);
            name = glb.pcodeinjectlib.getCallOtherTarget(injectid);
            UserPcodeOp * base = glb.userops.getOp(name);
            // This tag overrides the base functionality of a userop
            // so the core userop name and index may already be defined
            if (base == (UserPcodeOp*)0)
                throw new LowlevelError("Unknown userop name in <callotherfixup>: " + name);
            if (dynamic_cast<UnspecializedPcodeOp*>(base) == (UnspecializedPcodeOp*)0)  // Make sure the userop isn't used for some other purpose
                throw new LowlevelError("<callotherfixup> overloads userop with another purpose: " + name);
            useropindex = base.getIndex(); // Get the index from the core userop
        }
    }
}
