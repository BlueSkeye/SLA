using Sla.CORE;

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
        private uint injectid;         ///< The id of the injection object (to which this op maps)
        
        public InjectedUserOp(Architecture g, string nm,int ind, uint injid)
            : base(g, nm, ind)
        {
            injectid = injid;
        }

        /// Get the id of the injection object
        public uint getInjectId() => injectid;
    
        public override void decode(Sla.CORE.Decoder decoder)
        {
            injectid = (uint)glb.pcodeinjectlib.decodeInject("userop", "", InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE, decoder);
            name = glb.pcodeinjectlib.getCallOtherTarget((int)injectid);
            UserPcodeOp? @base = glb.userops.getOp(name);
            // This tag overrides the base functionality of a userop
            // so the core userop name and index may already be defined
            if (@base == (UserPcodeOp)null)
                throw new LowlevelError("Unknown userop name in <callotherfixup>: " + name);
            if ((@base as UnspecializedPcodeOp) == (UnspecializedPcodeOp)null)  // Make sure the userop isn't used for some other purpose
                throw new LowlevelError("<callotherfixup> overloads userop with another purpose: " + name);
            useropindex = @base.getIndex(); // Get the index from the core userop
        }
    }
}
