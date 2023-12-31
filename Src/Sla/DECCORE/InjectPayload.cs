﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief An active container for a set of p-code operations that can be injected into data-flow
    ///
    /// This is an abstract base class. Derived classes manage details of how the p-code
    /// is stored.  The methods provide access to the input/output parameter information,
    /// and the main injection is performed with inject().
    internal abstract class InjectPayload
    {
        public enum InjectionType
        {
            /// Injection that replaces a CALL
            CALLFIXUP_TYPE = 1,
            /// Injection that replaces a user-defined p-code op, CALLOTHER
            CALLOTHERFIXUP_TYPE = 2,
            /// Injection to patch up data-flow around the caller/callee boundary
            CALLMECHANISM_TYPE = 3,
            /// Injection running as a stand-alone p-code script
            EXECUTABLEPCODE_TYPE = 4
        }

        /// Formal name of the payload
        protected string name;
        /// Type of this payload: InjectPayload.InjectionType.CALLFIXUP_TYPE, InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE, etc.
        protected InjectionType type;
        /// True if the injection is generated dynamically
        protected bool dynamic;
        /// True if injected COPYs are considered \e incidental
        protected bool incidentalCopy;
        /// Number of parameters shifted in the original call
        protected int paramshift;
        /// List of input parameters to this payload
        internal List<InjectParameter> inputlist = new List<InjectParameter>();
        /// List of output parameters
        internal List<InjectParameter> output = new List<InjectParameter>();

        /// \brief Parse an \<input> or \<output> element describing an injection parameter
        ///
        /// \param decoder is the stream decoder
        /// \param name is used to pass back the parameter name
        /// \param size is used to pass back the parameter size
        protected static void decodeParameter(Sla.CORE.Decoder decoder, out string name, out uint size)
        {
            name = "";
            size = 0;
            uint elemId = decoder.openElement();
            while(true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_NAME)
                    name = decoder.readString();
                else if (attribId == AttributeId.ATTRIB_SIZE) {
                    size = (uint)decoder.readUnsignedInteger();
                }
            }
            decoder.closeElement(elemId);
            if (name.Length == 0)
                throw new LowlevelError("Missing inject parameter name");
        }

        /// Assign an index to parameters
        /// Input and output parameters are assigned a unique index
        internal void orderParameters()
        {
            int id = 0;
            for (int i = 0; i < inputlist.Count; ++i) {
                inputlist[i].index = id;
                id += 1;
            }
            for (int i = 0; i < output.Count; ++i) {
                output[i].index = id;
                id += 1;
            }
        }

        /// Parse the attributes of the current \<pcode> tag
        /// The \<pcode> element must be current and already opened.
        /// \param decoder is the stream decoder
        protected void decodePayloadAttributes(Decoder decoder)
        {
            paramshift = 0;
            dynamic = false;
            while(true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_PARAMSHIFT) {
                    paramshift = (int)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_DYNAMIC)
                    dynamic = decoder.readBool();
                else if (attribId == AttributeId.ATTRIB_INCIDENTALCOPY)
                    incidentalCopy = decoder.readBool();
                else if (attribId == AttributeId.ATTRIB_INJECT) {
                    string uponType = decoder.readString();
                    if (uponType == "uponentry")
                        name = name + "@@inject_uponentry";
                    else
                        name = name + "@@inject_uponreturn";
                }
            }
        }

        /// Parse any \<input> or \<output> children of current \<pcode> tag
        /// Elements are processed until the first child that isn't an \<input> or \<output> tag
        /// is encountered. The \<pcode> element must be current and already opened.
        /// \param decoder is the stream decoder
        protected void decodePayloadParams(Sla.CORE.Decoder decoder)
        {
            while(true) {
                uint subId = decoder.peekElement();
                if (subId == ElementId.ELEM_INPUT) {
                    string paramName;
                    uint size;
                    decodeParameter(decoder, out paramName, out size);
                    inputlist.Add(new InjectParameter(paramName, size));
                }
                else if (subId == ElementId.ELEM_OUTPUT) {
                    string paramName;
                    uint size;
                    decodeParameter(decoder, out paramName, out size);
                    output.Add(new InjectParameter(paramName, size));
                }
                else
                    break;
            }
            orderParameters();
        }

        public InjectPayload(string nm, InjectionType tp)
        {
            name = nm;
            type = tp;
            paramshift = 0;
            dynamic = false;
            incidentalCopy = false;
        }

        /// Get the number of parameters shifted
        internal int getParamShift() => paramshift;

        /// Return \b true if p-code in the injection is generated dynamically
        internal bool isDynamic() => dynamic;

        /// Return \b true if any injected COPY is considered \e incidental
        internal bool isIncidentalCopy() => incidentalCopy;

        /// Return the number of input parameters
        internal int sizeInput() => inputlist.Count;

        /// Return the number of output parameters
        internal int sizeOutput() => output.Count;

        /// Get the i-th input parameter
        internal InjectParameter getInput(int i) => inputlist[i];

        /// Get the i-th output parameter
        internal InjectParameter getOutput(int i) => output[i];

        ~InjectPayload()
        {
        }

        /// Perform the injection of \b this payload into data-flow.
        /// P-code operations representing \b this payload are copied into the
        /// controlling analysis context. The provided PcodeEmit object dictates exactly
        /// where the PcodeOp and Varnode objects are inserted and to what container.
        /// An InjectContext object specifies how placeholder elements become concrete Varnodes
        /// in the appropriate context.
        /// \param context is the provided InjectConject object
        /// \param emit is the provovided PcodeEmit object
        internal abstract void inject(InjectContext context, PcodeEmit emit);

        /// Decode \b this payload from a stream
        internal abstract void decode(Sla.CORE.Decoder decoder);

        /// Print the p-code ops of the injection to a stream (for debugging)
        internal abstract void printTemplate(TextWriter s);

        /// Return the name of the injection
        internal virtual string getName() => name;

        /// Return the type of injection (CALLFIXUP_TYPE, InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE, etc.)
        internal virtual InjectionType getType() => type;

        /// Return a string describing the \e source of the injection (.cspec, prototype model, etc.)
        internal abstract string getSource();
    }
}
