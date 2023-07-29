using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class PcodeInjectLibrarySleigh : PcodeInjectLibrary
    {
        private SleighBase slgh;
        private List<OpBehavior> inst;
        private InjectContextSleigh contextCache;

        private int4 registerDynamicInject(InjectPayload payload)
        {
            int4 id = injection.size();
            injection.push_back(payload);
            return id;
        }

        /// \brief Force a payload to be dynamic for debug purposes
        ///
        /// Debug information may include inject information for payloads that aren't dynamic.
        /// We substitute a dynamic payload so that analysis uses the debug info to inject, rather
        /// than the hard-coded payload information.
        /// \param injectid is the id of the payload to treat dynamic
        /// \return the new dynamic payload object
        private InjectPayloadDynamic forceDebugDynamic(int4 injectid)
        {
            InjectPayload* oldPayload = injection[injectid];
            InjectPayloadDynamic* newPayload = new InjectPayloadDynamic(glb, oldPayload->getName(), oldPayload->getType());
            delete oldPayload;
            injection[injectid] = newPayload;
            return newPayload;
        }

        private void parseInject(InjectPayload payload)
        {
            if (payload->isDynamic())
                return;
            if (slgh == (SleighBase*)0) { // Make sure we have the sleigh AddrSpaceManager
                slgh = (SleighBase*)glb->translate;
                if (slgh == (SleighBase*)0)
                    throw new LowlevelError("Registering pcode snippet before language is instantiated");
            }
            if (contextCache.pos == (ParserContext*)0)
            {   // Make sure we have a context
                contextCache.pos = new ParserContext((ContextCache*)0, (Translate*)0);
                contextCache.pos->initialize(8, 8, slgh->getConstantSpace());
            }
            PcodeSnippet compiler(slgh);
            //  compiler.clear();			// Not necessary unless we reuse
            for (int4 i = 0; i < payload->sizeInput(); ++i)
            {
                InjectParameter & param(payload->getInput(i));
                compiler.addOperand(param.getName(), param.getIndex());
            }
            for (int4 i = 0; i < payload->sizeOutput(); ++i)
            {
                InjectParameter & param(payload->getOutput(i));
                compiler.addOperand(param.getName(), param.getIndex());
            }
            if (payload->getType() == InjectPayload::EXECUTABLEPCODE_TYPE)
            {
                compiler.setUniqueBase(0x2000); // Don't need to deconflict with anything other injects
                ExecutablePcodeSleigh* sleighpayload = (ExecutablePcodeSleigh*)payload;
                istringstream s(sleighpayload->parsestring);
                if (!compiler.parseStream(s))
                    throw new LowlevelError(payload->getSource() + ": Unable to compile pcode: " + compiler.getErrorMessage());
                sleighpayload->tpl = compiler.releaseResult();
                sleighpayload->parsestring = "";        // No longer need the memory
            }
            else
            {
                compiler.setUniqueBase(tempbase);
                InjectPayloadSleigh* sleighpayload = (InjectPayloadSleigh*)payload;
                istringstream s(sleighpayload->parsestring);
                if (!compiler.parseStream(s))
                    throw new LowlevelError(payload->getSource() + ": Unable to compile pcode: " + compiler.getErrorMessage());
                tempbase = compiler.getUniqueBase();
                sleighpayload->tpl = compiler.releaseResult();
                sleighpayload->parsestring = "";        // No longer need the memory
            }
        }

        protected override int4 allocateInject(string sourceName, string name,int4 type)
        {
            int4 injectid = injection.size();
            if (type == InjectPayload::CALLFIXUP_TYPE)
                injection.push_back(new InjectPayloadCallfixup(sourceName));
            else if (type == InjectPayload::CALLOTHERFIXUP_TYPE)
                injection.push_back(new InjectPayloadCallother(sourceName));
            else if (type == InjectPayload::EXECUTABLEPCODE_TYPE)
                injection.push_back(new ExecutablePcodeSleigh(glb, sourceName, name));
            else
                injection.push_back(new InjectPayloadSleigh(sourceName, name, type));
            return injectid;
        }

        protected override void registerInject(int4 injectid)
        {
            InjectPayload* payload = injection[injectid];
            if (payload->isDynamic())
            {
                InjectPayload* sub = new InjectPayloadDynamic(glb, payload->getName(), payload->getType());
                delete payload;
                payload = sub;
                injection[injectid] = payload;
            }
            switch (payload->getType())
            {
                case InjectPayload::CALLFIXUP_TYPE:
                    registerCallFixup(payload->getName(), injectid);
                    parseInject(payload);
                    break;
                case InjectPayload::CALLOTHERFIXUP_TYPE:
                    registerCallOtherFixup(payload->getName(), injectid);
                    parseInject(payload);
                    break;
                case InjectPayload::CALLMECHANISM_TYPE:
                    registerCallMechanism(payload->getName(), injectid);
                    parseInject(payload);
                    break;
                case InjectPayload::EXECUTABLEPCODE_TYPE:
                    registerExeScript(payload->getName(), injectid);
                    parseInject(payload);
                    break;
                default:
                    throw new LowlevelError("Unknown p-code inject type");
            }
        }

        public PcodeInjectLibrarySleigh(Architecture g)
            : base(g, g->translate->getUniqueStart(Translate::INJECT))
        {
            slgh = (SleighBase*)g->translate;
            contextCache.glb = g;
        }

        public override void decodeDebug(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_INJECTDEBUG);
            for (; ; )
            {
                uint4 subId = decoder.openElement();
                if (subId != ELEM_INJECT) break;
                string name = decoder.readString(ATTRIB_NAME);
                int4 type = decoder.readSignedInteger(ATTRIB_TYPE);
                int4 id = getPayloadId(type, name);
                InjectPayloadDynamic* payload = dynamic_cast<InjectPayloadDynamic*>(getPayload(id));
                if (payload == (InjectPayloadDynamic*)0)
                {
                    payload = forceDebugDynamic(id);
                }
                payload->decodeEntry(decoder);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        protected override int4 manualCallFixup(string name, string snippetstring)
        {
            string sourceName = "(manual callfixup name=\"" + name + "\")";
            int4 injectid = allocateInject(sourceName, name, InjectPayload::CALLFIXUP_TYPE);
            InjectPayloadSleigh* payload = (InjectPayloadSleigh*)getPayload(injectid);
            payload->parsestring = snippetstring;
            registerInject(injectid);
            return injectid;
        }

        protected override int4 manualCallOtherFixup(string name, string outname, List<string> inname,
            string snippet)
        {
            string sourceName = "<manual callotherfixup name=\"" + name + "\")";
            int4 injectid = allocateInject(sourceName, name, InjectPayload::CALLOTHERFIXUP_TYPE);
            InjectPayloadSleigh* payload = (InjectPayloadSleigh*)getPayload(injectid);
            for (int4 i = 0; i < inname.size(); ++i)
                payload->inputlist.push_back(InjectParameter(inname[i], 0));
            if (outname.size() != 0)
                payload->output.push_back(InjectParameter(outname, 0));
            payload->orderParameters();
            payload->parsestring = snippet;
            registerInject(injectid);
            return injectid;
        }

        protected override InjectContext getCachedContext() => contextCache;

        protected override List<OpBehavior> getBehaviors()
        {
            if (inst.empty())
                glb->collectBehaviors(inst);
            return inst;
        }
    }
}
