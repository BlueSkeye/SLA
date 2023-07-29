using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class InjectPayloadCallother : InjectPayloadSleigh
    {
        public InjectPayloadCallother(string sourceName)
            : base(sourceName,"unknown", CALLOTHERFIXUP_TYPE)
        {
        }

        public override void decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_CALLOTHERFIXUP);
            name = decoder.readString(ATTRIB_TARGETOP);
            uint4 subId = decoder.openElement();
            if (subId != ELEM_PCODE)
                throw new LowlevelError("<callotherfixup> does not contain a <pcode> tag");
            decodePayloadAttributes(decoder);
            decodePayloadParams(decoder);
            decodeBody(decoder);
            decoder.closeElement(subId);
            decoder.closeElement(elemId);
        }
    }
}
