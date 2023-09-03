using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class InjectPayloadCallother : InjectPayloadSleigh
    {
        public InjectPayloadCallother(string sourceName)
            : base(sourceName,"unknown", InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE)
        {
        }

        internal override void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_CALLOTHERFIXUP);
            name = decoder.readString(AttributeId.ATTRIB_TARGETOP);
            uint subId = decoder.openElement();
            if (subId != ELEM_PCODE)
                throw new CORE.LowlevelError("<callotherfixup> does not contain a <pcode> tag");
            decodePayloadAttributes(decoder);
            decodePayloadParams(decoder);
            decodeBody(decoder);
            decoder.closeElement(subId);
            decoder.closeElement(elemId);
        }
    }
}
