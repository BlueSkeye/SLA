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
        internal string parsestring;
        internal ConstructTpl? tpl;
        
        public ExecutablePcodeSleigh(Architecture g, string src, string nm)
            : base(g, src, nm)
        {
            tpl = (ConstructTpl)null;
        }

        ~ExecutablePcodeSleigh()
        {
            //if (tpl != (ConstructTpl)null)
            //    delete tpl;
        }

        internal override void inject(InjectContext context, PcodeEmit emit)
        {
            InjectContextSleigh con = (InjectContextSleigh)context;

            con.cacher.clear();
            con.pos.setAddr(con.baseaddr);
            con.pos.setNaddr(con.nextaddr);
            con.pos.setCalladdr(con.calladdr);

            ParserWalkerChange walker = new ParserWalkerChange(con.pos);
            con.pos.deallocateState(walker);
            InjectPayloadSleigh.setupParameters(con, walker, inputlist, output, getSource());
            // delayslot and crossbuild directives are not allowed in snippets, so we don't need the DisassemblyCache
            // and we don't need a unique allocation mask
            SleighBuilder builder = new SleighBuilder(walker, (DisassemblyCache)null, con.cacher,
                con.glb.getConstantSpace(), con.glb.getUniqueSpace(),0);
            builder.build(tpl, -1);
            con.cacher.resolveRelatives();
            con.cacher.emit(con.baseaddr, emit);
        }

        internal override void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement();
            if (   elemId != ElementId.ELEM_PCODE
                && elemId != ElementId.ELEM_CASE_PCODE
                && elemId != ElementId.ELEM_ADDR_PCODE
                && elemId != ElementId.ELEM_DEFAULT_PCODE
                && elemId != ElementId.ELEM_SIZE_PCODE)
            {
                throw new DecoderError("Expecting <pcode>, <case_pcode>, <addr_pcode>, <default_pcode>, or <size_pcode>");
            }
            decodePayloadAttributes(decoder);
            decodePayloadParams(decoder);
            uint subId = decoder.openElement(ElementId.ELEM_BODY);
            parsestring = decoder.readString(AttributeId.ATTRIB_CONTENT);
            decoder.closeElement(subId);
            decoder.closeElement(elemId);
        }

        internal override void printTemplate(TextWriter s)
        {
            tpl.saveXml(s, -1);
        }
    }
}
