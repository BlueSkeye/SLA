﻿using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class InjectPayloadCallfixup : InjectPayloadSleigh
    {
        private List<string> targetSymbolNames = new List<string>();
        
        public InjectPayloadCallfixup(string sourceName)
            : base(sourceName,"unknown", CALLFIXUP_TYPE)
        {
        }

        public override void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ELEM_CALLFIXUP);
            name = decoder.readString(ATTRIB_NAME);
            bool pcodeSubtag = false;

            for (; ; )
            {
                uint subId = decoder.openElement();
                if (subId == 0) break;
                if (subId == ELEM_PCODE)
                {
                    decodePayloadAttributes(decoder);
                    decodePayloadParams(decoder);
                    decodeBody(decoder);
                    pcodeSubtag = true;
                }
                else if (subId == ELEM_TARGET)
                    targetSymbolNames.Add(decoder.readString(ATTRIB_NAME));
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
            if (!pcodeSubtag)
                throw new LowlevelError("<callfixup> is missing <pcode> subtag: " + name);
        }
    }
}
