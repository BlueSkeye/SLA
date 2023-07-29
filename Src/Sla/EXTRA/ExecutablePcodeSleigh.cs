using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ExecutablePcodeSleigh : ExecutablePcode
    {
        // friend class PcodeInjectLibrarySleigh;
        protected string parsestring;
        protected ConstructTpl tpl;
        
        public ExecutablePcodeSleigh(Architecture g, string src, string nm)
            : base(g, src, nm)
        {
            tpl = (ConstructTpl*)0;
        }

        ~ExecutablePcodeSleigh()
        {
            if (tpl != (ConstructTpl*)0)
                delete tpl;
        }

        public override void inject(InjectContext context, PcodeEmit emit)
        {
            InjectContextSleigh & con((InjectContextSleigh &)context);

            con.cacher.clear();

            con.pos->setAddr(con.baseaddr);
            con.pos->setNaddr(con.nextaddr);
            con.pos->setCalladdr(con.calladdr);

            ParserWalkerChange walker(con.pos);
            con.pos->deallocateState(walker);
            InjectPayloadSleigh::setupParameters(con, walker, inputlist, output, getSource());
            // delayslot and crossbuild directives are not allowed in snippets, so we don't need the DisassemblyCache
            // and we don't need a unique allocation mask
            SleighBuilder builder(&walker,(DisassemblyCache*)0,&con.cacher,con.glb->getConstantSpace(),con.glb->getUniqueSpace(),0);
            builder.build(tpl, -1);
            con.cacher.resolveRelatives();
            con.cacher.emit(con.baseaddr, &emit);
        }

        public override void decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement();
            if (elemId != ELEM_PCODE && elemId != ELEM_CASE_PCODE &&
                elemId != ELEM_ADDR_PCODE && elemId != ELEM_DEFAULT_PCODE && elemId != ELEM_SIZE_PCODE)
                throw DecoderError("Expecting <pcode>, <case_pcode>, <addr_pcode>, <default_pcode>, or <size_pcode>");
            decodePayloadAttributes(decoder);
            decodePayloadParams(decoder);
            uint4 subId = decoder.openElement(ELEM_BODY);
            parsestring = decoder.readString(ATTRIB_CONTENT);
            decoder.closeElement(subId);
            decoder.closeElement(elemId);
        }

        public override void printTemplate(TextWriter s)
        {
            tpl->saveXml(s, -1);
        }
    }
}
