using ghidra;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        /// Type of this payload: CALLFIXUP_TYPE, CALLOTHERFIXUP_TYPE, etc.
        protected int4 type;
        /// True if the injection is generated dynamically
        protected bool dynamic;
        /// True if injected COPYs are considered \e incidental
        protected bool incidentalCopy;
        /// Number of parameters shifted in the original call
        protected int4 paramshift;
        /// List of input parameters to this payload
        protected List<InjectParameter> inputlist;
        /// List of output parameters
        protected List<InjectParameter> output;

        /// \brief Parse an \<input> or \<output> element describing an injection parameter
        ///
        /// \param decoder is the stream decoder
        /// \param name is used to pass back the parameter name
        /// \param size is used to pass back the parameter size
        protected static void decodeParameter(Decoder decoder, string name, uint4 size)
        {
            name = "";
            size = 0;
            uint4 elemId = decoder.openElement();
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_NAME)
                    name = decoder.readString();
                else if (attribId == ATTRIB_SIZE)
                {
                    size = decoder.readUnsignedInteger();
                }
            }
            decoder.closeElement(elemId);
            if (name.size() == 0)
                throw LowlevelError("Missing inject parameter name");
        }

        /// Assign an index to parameters
        /// Input and output parameters are assigned a unique index
        protected void orderParameters()
        {
            int4 id = 0;
            for (int4 i = 0; i < inputlist.size(); ++i)
            {
                inputlist[i].index = id;
                id += 1;
            }
            for (int4 i = 0; i < output.size(); ++i)
            {
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
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_PARAMSHIFT)
                {
                    paramshift = decoder.readSignedInteger();
                }
                else if (attribId == ATTRIB_DYNAMIC)
                    dynamic = decoder.readBool();
                else if (attribId == ATTRIB_INCIDENTALCOPY)
                    incidentalCopy = decoder.readBool();
                else if (attribId == ATTRIB_INJECT)
                {
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
        protected void decodePayloadParams(Decoder decoder)
        {
            for (; ; )
            {
                uint4 subId = decoder.peekElement();
                if (subId == ELEM_INPUT)
                {
                    string paramName;
                    uint4 size;
                    decodeParameter(decoder, paramName, size);
                    inputlist.push_back(InjectParameter(paramName, size));
                }
                else if (subId == ELEM_OUTPUT)
                {
                    string paramName;
                    uint4 size;
                    decodeParameter(decoder, paramName, size);
                    output.push_back(InjectParameter(paramName, size));
                }
                else
                    break;
            }
            orderParameters();
        }

        public InjectPayload(string nm,int4 tp)
        {
            name=nm;
            type=tp;
            paramshift=0;
            dynamic = false;
            incidentalCopy = false;
        }   ///< Construct for use with decode

        /// Get the number of parameters shifted
        protected int4 getParamShift() => paramshift;

        /// Return \b true if p-code in the injection is generated dynamically
        protected bool isDynamic() => dynamic;

        /// Return \b true if any injected COPY is considered \e incidental
        protected bool isIncidentalCopy() => incidentalCopy;

        /// Return the number of input parameters
        protected int4 sizeInput() => inputlist.size();

        /// Return the number of output parameters
        protected int4 sizeOutput() => output.size();

        /// Get the i-th input parameter
        protected InjectParameter getInput(int4 i) => inputlist[i];

        /// Get the i-th output parameter
        protected InjectParameter getOutput(int4 i) => output[i];

        ~InjectPayload()
        {
        }

        /// Perform the injection of \b this payload into data-flow.
        ///
        /// P-code operations representing \b this payload are copied into the
        /// controlling analysis context. The provided PcodeEmit object dictates exactly
        /// where the PcodeOp and Varnode objects are inserted and to what container.
        /// An InjectContext object specifies how placeholder elements become concrete Varnodes
        /// in the appropriate context.
        /// \param context is the provided InjectConject object
        /// \param emit is the provovided PcodeEmit object
        protected abstract void inject(InjectContext context, PcodeEmit emit);

        /// Decode \b this payload from a stream
        protected abstract void decode(Sla.CORE.Decoder decoder);

        /// Print the p-code ops of the injection to a stream (for debugging)
        protected abstract void printTemplate(TextWriter s);

        /// Return the name of the injection
        protected abstract string getName() => name;

        /// Return the type of injection (CALLFIXUP_TYPE, CALLOTHERFIXUP_TYPE, etc.)
        protected abstract int4 getType() => type;

        /// Return a string describing the \e source of the injection (.cspec, prototype model, etc.)
        protected abstract string getSource();
    }
}
