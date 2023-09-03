using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System.Text;

namespace Sla.EXTRA
{
    internal class PcodeInjectLibrarySleigh : PcodeInjectLibrary
    {
        private SleighBase? slgh;
        private List<OpBehavior> inst;
        private InjectContextSleigh contextCache;

        private int registerDynamicInject(InjectPayload payload)
        {
            int id = injection.size();
            injection.Add(payload);
            return id;
        }

        /// \brief Force a payload to be dynamic for debug purposes
        ///
        /// Debug information may include inject information for payloads that aren't dynamic.
        /// We substitute a dynamic payload so that analysis uses the debug info to inject, rather
        /// than the hard-coded payload information.
        /// \param injectid is the id of the payload to treat dynamic
        /// \return the new dynamic payload object
        private InjectPayloadDynamic forceDebugDynamic(int injectid)
        {
            InjectPayload oldPayload = injection[injectid];
            InjectPayloadDynamic newPayload = new InjectPayloadDynamic(glb, oldPayload.getName(),
                oldPayload.getType());
            // delete oldPayload;
            injection[injectid] = newPayload;
            return newPayload;
        }

        private void parseInject(InjectPayload payload)
        {
            if (payload.isDynamic())
                return;
            if (slgh == (SleighBase)null) { // Make sure we have the sleigh AddrSpaceManager
                Parsing.slgh = (SleighBase)glb.translate;
                if (Parsing.slgh == (SleighBase)null)
                    throw new CORE.LowlevelError("Registering pcode snippet before language is instantiated");
            }
            if (contextCache.pos == (ParserContext)null) {
                // Make sure we have a context
                contextCache.pos = new ParserContext((ContextCache)null, (Translate)null);
                contextCache.pos.initialize(8, 8, Parsing.slgh.getConstantSpace());
            }
            PcodeSnippet compiler = new PcodeSnippet(Parsing.slgh);
            //  compiler.clear();			// Not necessary unless we reuse
            for (int i = 0; i < payload.sizeInput(); ++i) {
                InjectParameter param = payload.getInput(i);
                compiler.addOperand(param.getName(), param.getIndex());
            }
            for (int i = 0; i < payload.sizeOutput(); ++i) {
                InjectParameter param = payload.getOutput(i);
                compiler.addOperand(param.getName(), param.getIndex());
            }
            if (payload.getType() == InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE) {
                compiler.setUniqueBase(0x2000); // Don't need to deconflict with anything other injects
                ExecutablePcodeSleigh sleighpayload = (ExecutablePcodeSleigh)payload;
                TextReader s = new StringReader(sleighpayload.parsestring);
                if (!compiler.parseStream(s))
                    throw new CORE.LowlevelError(
                        $"{payload.getSource()}: Unable to compile pcode: {compiler.getErrorMessage()}");
                sleighpayload.tpl = compiler.releaseResult();
                sleighpayload.parsestring = "";        // No longer need the memory
            }
            else {
                compiler.setUniqueBase(tempbase);
                InjectPayloadSleigh sleighpayload = (InjectPayloadSleigh)payload;
                TextReader s = new StringReader(sleighpayload.parsestring);
                if (!compiler.parseStream(s))
                    throw new CORE.LowlevelError(
                        $"{payload.getSource()}: Unable to compile pcode: {compiler.getErrorMessage()}");
                tempbase = compiler.getUniqueBase();
                sleighpayload.tpl = compiler.releaseResult();
                // No longer need the memory
                sleighpayload.parsestring = string.Empty;
            }
        }

        protected override int allocateInject(string sourceName, string name, InjectPayload.InjectionType type)
        {
            int injectid = injection.size();
            if (type == InjectPayload.InjectionType.CALLFIXUP_TYPE)
                injection.Add(new InjectPayloadCallfixup(sourceName));
            else if (type == InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE)
                injection.Add(new InjectPayloadCallother(sourceName));
            else if (type == InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE)
                injection.Add(new ExecutablePcodeSleigh(glb, sourceName, name));
            else
                injection.Add(new InjectPayloadSleigh(sourceName, name, type));
            return injectid;
        }

        protected override void registerInject(int injectid)
        {
            InjectPayload payload = injection[injectid];
            if (payload.isDynamic()) {
                InjectPayload sub = new InjectPayloadDynamic(glb, payload.getName(), payload.getType());
                // delete payload;
                payload = sub;
                injection[injectid] = payload;
            }
            switch (payload.getType())
            {
                case InjectPayload.InjectionType.CALLFIXUP_TYPE:
                    registerCallFixup(payload.getName(), injectid);
                    parseInject(payload);
                    break;
                case InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE:
                    registerCallOtherFixup(payload.getName(), injectid);
                    parseInject(payload);
                    break;
                case InjectPayload.InjectionType.CALLMECHANISM_TYPE:
                    registerCallMechanism(payload.getName(), injectid);
                    parseInject(payload);
                    break;
                case InjectPayload.InjectionType.EXECUTABLEPCODE_TYPE:
                    registerExeScript(payload.getName(), injectid);
                    parseInject(payload);
                    break;
                default:
                    throw new CORE.LowlevelError("Unknown p-code inject type");
            }
        }

        public PcodeInjectLibrarySleigh(Architecture g)
            : base(g, g.translate.getUniqueStart(Translate.UniqueLayout.INJECT))
        {
            slgh = (SleighBase)g.translate;
            contextCache.glb = g;
        }

        public override void decodeDebug(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_INJECTDEBUG);
            while(true) {
                uint subId = decoder.openElement();
                if (subId != ElementId.ELEM_INJECT) break;
                string name = decoder.readString(AttributeId.ATTRIB_NAME);
                int type = (int)decoder.readSignedInteger(AttributeId.ATTRIB_TYPE);
                int id = getPayloadId(type, name);
                InjectPayloadDynamic? payload = getPayload(id) as InjectPayloadDynamic;
                if (payload == (InjectPayloadDynamic)null) {
                    payload = forceDebugDynamic(id);
                }
                payload.decodeEntry(decoder);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        public override int manualCallFixup(string name, string snippetstring)
        {
            string sourceName = "(manual callfixup name=\"" + name + "\")";
            int injectid = allocateInject(sourceName, name, InjectPayload.InjectionType.CALLFIXUP_TYPE);
            InjectPayloadSleigh payload = (InjectPayloadSleigh)getPayload(injectid);
            payload.parsestring = snippetstring;
            registerInject(injectid);
            return injectid;
        }

        public override int manualCallOtherFixup(string name, string outname, List<string> inname,
            string snippet)
        {
            string sourceName = "<manual callotherfixup name=\"" + name + "\")";
            int injectid = allocateInject(sourceName, name, InjectPayload.InjectionType.CALLOTHERFIXUP_TYPE);
            InjectPayloadSleigh payload = (InjectPayloadSleigh)getPayload(injectid);
            for (int i = 0; i < inname.size(); ++i)
                payload.inputlist.Add(new InjectParameter(inname[i], 0));
            if (outname.Length != 0)
                payload.output.Add(new InjectParameter(outname, 0));
            payload.orderParameters();
            payload.parsestring = snippet;
            registerInject(injectid);
            return injectid;
        }

        public override InjectContext getCachedContext() => contextCache;

        public override List<OpBehavior> getBehaviors()
        {
            if (inst.empty())
                glb.collectBehaviors(inst);
            return inst;
        }
    }
}
