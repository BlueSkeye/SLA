using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A tracked register (Varnode) and the value it contains
    /// This is the object returned when querying for tracked registers,
    /// via ContextDatabase::getTrackedSet().  It holds the storage details of the register and
    /// the actual value it holds at the point of the query.
    internal struct TrackedContext
    {
        /// Storage details of the register being tracked
        internal VarnodeData loc;
        /// The value of the register
        internal ulong val;

        /// Encode \b this to a stream
        /// The register storage and value are encoded as a \<set> element.
        /// \param encoder is the stream encoder
        internal void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_SET);
            loc.space.encodeAttributes(encoder, loc.offset, (int)loc.size);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_VAL, val);
            encoder.closeElement(ElementId.ELEM_SET);
        }

        /// Decode \b this from a stream
        /// Parse a \<set> element to fill in the storage and value details.
        /// \param decoder is the stream decoder
        internal void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_SET);
            this.loc = VarnodeData.decodeFromAttributes(decoder);
            val = decoder.readUnsignedInteger(AttributeId.ATTRIB_VAL);
            decoder.closeElement(elemId);
        }
    }
}
