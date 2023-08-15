using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class InjectPayloadDynamic : InjectPayload
    {
        private Architecture glb;
        // Map from address to specific inject
        private Dictionary<Address, Document> addrMap = new Dictionary<Address, Document>();
        
        public InjectPayloadDynamic(Architecture g, string nm, InjectionType tp)
            : base(nm, tp)
        {
            glb = g;
            dynamic = true;
        }
        
        ~InjectPayloadDynamic()
        {
            //Dictionary<Address, Document*>::iterator iter;
            //for (iter = addrMap.begin(); iter != addrMap.end(); ++iter)
            //    delete(*iter).second;
        }

        public void decodeEntry(Decoder decoder)
        {
            Address addr = Address.decode(decoder);
            uint subId = decoder.openElement(ElementId.ELEM_PAYLOAD);
            StringReader s = new StringReader(decoder.readString(AttributeId.ATTRIB_CONTENT));
            try {
                Document doc = Xml.xml_tree(s);
                //Dictionary<Address, Document*>::iterator iter = addrMap.find(addr);
                //if (iter != addrMap.end())
                //    delete(*iter).second;       // Delete any preexisting document
                addrMap[addr] = doc;
            }
            catch (DecoderError) {
                throw new CORE.LowlevelError("Error decoding dynamic payload");
            }
            decoder.closeElement(subId);
        }

        internal override void inject(InjectContext context, PcodeEmit emit)
        {
            Document document;
            
            if (!addrMap.TryGetValue(context.baseaddr, out document))
                throw new CORE.LowlevelError("Missing dynamic inject");
            Element el = document.getRoot() ?? throw new BugException();
            XmlDecode decoder = new XmlDecode(glb.translate, el);
            uint rootId = decoder.openElement(ElementId.ELEM_INST);
            Address addr = Address.decode(decoder);
            while (decoder.peekElement() != 0)
                emit.decodeOp(addr, decoder);
            decoder.closeElement(rootId);
        }

        internal override void decode(Sla.CORE.Decoder decoder)
        {
            throw new CORE.LowlevelError("decode not supported for InjectPayloadDynamic");
        }

        internal override void printTemplate(TextWriter s)
        {
            s.Write("dynamic");
        }

        internal override string getSource() => "dynamic";
    }
}
