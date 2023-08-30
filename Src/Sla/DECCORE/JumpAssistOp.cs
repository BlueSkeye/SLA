using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A user defined p-code op for assisting the recovery of jump tables.
    ///
    /// An instance of this class refers to p-code script(s)
    /// that describe how to parse the jump table from the load image. Possible scripts include:
    ///  - (if present) \b index2case describes how to get case values from an index 0..size-1
    ///  - \b index2addr describes how to get address values from the same index range
    ///  - \b defaultaddr describes how to calculate the switch's default address
    ///  - (if present) \b calcsize recovers the number of indices in the table
    ///
    /// This class stores injection ids. The scripts themselves are managed by PcodeInjectLibrary.
    internal class JumpAssistOp : UserPcodeOp
    {
        /// Id of p-code script performing index2case (== -1 if no script and index==case)
        private int index2case;
        /// Id of p-code script performing index2addr (must be present)
        private int index2addr;
        /// Id of p-code script performing calculation of default address (must be present)
        private int defaultaddr;
        /// Id of p-code script that calculates number of indices (== -1 if no script)
        private int calcsize;

        /// \param g is the Architecture owning this set of jump assist scripts
        public JumpAssistOp(Architecture g)
            : base(g,"",0)
        {
            index2case = -1;
            index2addr = -1;
            defaultaddr = -1;
            calcsize = -1;
        }

        /// Get the injection id for \b index2case
        public int getIndex2Case() => index2case;

        /// Get the injection id for \b index2addr
        public int getIndex2Addr() => index2addr;

        /// Get the injection id for \b defaultaddr
        public int getDefaultAddr() => defaultaddr;

        /// Get the injection id for \b calcsize
        public int getCalcSize() => calcsize;

        public override void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_JUMPASSIST);
            name = decoder.readString(AttributeId.ATTRIB_NAME);
            index2case = -1;    // Mark as not present until we see a tag
            index2addr = -1;
            defaultaddr = -1;
            calcsize = -1;
            while(true) {
                ElementId subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_CASE_PCODE) {
                    if (index2case != -1)
                        throw new LowlevelError("Too many <case_pcode> tags");
                    index2case = glb.pcodeinjectlib.decodeInject("jumpassistop", name + "_index2case",
                                           InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE, decoder);
                }
                else if (subId == ElementId.ELEM_ADDR_PCODE) {
                    if (index2addr != -1)
                        throw new LowlevelError("Too many <addr_pcode> tags");
                    index2addr = glb.pcodeinjectlib.decodeInject("jumpassistop", name + "_index2addr",
                                           InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE, decoder);
                }
                else if (subId == ElementId.ELEM_DEFAULT_PCODE) {
                    if (defaultaddr != -1)
                        throw new LowlevelError("Too many <default_pcode> tags");
                    defaultaddr = glb.pcodeinjectlib.decodeInject("jumpassistop", name + "_defaultaddr",
                                            InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE, decoder);
                }
                else if (subId == ElementId.ELEM_SIZE_PCODE) {
                    if (calcsize != -1)
                        throw new LowlevelError("Too many <size_pcode> tags");
                    calcsize = glb.pcodeinjectlib.decodeInject("jumpassistop", name + "_calcsize",
                                         InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE, decoder);
                }
            }
            decoder.closeElement(elemId);

            if (index2addr == -1)
                throw new LowlevelError("userop: " + name + " is missing <addr_pcode>");
            if (defaultaddr == -1)
                throw new LowlevelError("userop: " + name + " is missing <default_pcode>");
            UserPcodeOp? @base = glb.userops.getOp(name);
            // This tag overrides the base functionality of a userop
            // so the core userop name and index may already be defined
            if (@base == (UserPcodeOp)null)
                throw new LowlevelError("Unknown userop name in <jumpassist>: " + name);
            if ((@base as UnspecializedPcodeOp) == (UnspecializedPcodeOp)null)  // Make sure the userop isn't used for some other purpose
                throw new LowlevelError("<jumpassist> overloads userop with another purpose: " + name);
            useropindex = @base.getIndex(); // Get the index from the core userop
        }
    }
}
