using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal abstract class InjectPayloadDynamic : InjectPayload
    {
        private Architecture glb;
        // Map from address to specific inject
        private Dictionary<Address, Document> addrMap = new Dictionary<Address, Document>();
        
        public InjectPayloadDynamic(Architecture g, string nm,int4 tp)
            : base(nm, tp)
        {
            glb = g;
            dynamic = true;
        }
        
        ~InjectPayloadDynamic()
        {
            map<Address, Document*>::iterator iter;
            for (iter = addrMap.begin(); iter != addrMap.end(); ++iter)
                delete(*iter).second;
        }

        public void decodeEntry(Decoder decoder)
        {
            Address addr = Address::decode(decoder);
            uint4 subId = decoder.openElement(ELEM_PAYLOAD);
            istringstream s(decoder.readString(ATTRIB_CONTENT));
            try
            {
                Document* doc = xml_tree(s);
                map<Address, Document*>::iterator iter = addrMap.find(addr);
                if (iter != addrMap.end())
                    delete(*iter).second;       // Delete any preexisting document
                addrMap[addr] = doc;
            }
            catch (DecoderError err) {
                throw new LowlevelError("Error decoding dynamic payload");
            }
            decoder.closeElement(subId);
        }

        protected override void inject(InjectContext context, PcodeEmit emit)
        {
            map<Address, Document*>::const_iterator eiter = addrMap.find(context.baseaddr);
            if (eiter == addrMap.end())
                throw new LowlevelError("Missing dynamic inject");
            Element el = (*eiter).second.getRoot();
            XmlDecode decoder(glb.translate, el);
            uint4 rootId = decoder.openElement(ELEM_INST);
            Address addr = Address::decode(decoder);
            while (decoder.peekElement() != 0)
                emit.decodeOp(addr, decoder);
            decoder.closeElement(rootId);
        }

        protected override void decode(Decoder decoder)
        {
            throw new LowlevelError("decode not supported for InjectPayloadDynamic");
        }

        protected override void printTemplate(TextWriter s)
        {
            s << "dynamic";
        }

        protected override string getSource() => "dynamic";
    }
}
