using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;

namespace Sla.EXTRA
{
    internal class InjectPayloadSleigh : InjectPayload
    {
        // friend class PcodeInjectLibrarySleigh;
        private ConstructTpl? tpl;
        internal string parsestring;
        private string source;

        ///< Parse the <body> tag
        /// The content is read as raw p-code source
        /// \param decoder is the XML stream decoder
        protected void decodeBody(Decoder decoder)
        {
            ElementId elemId = decoder.openElement();       // Tag may not be present
            if (elemId == ElementId.ELEM_BODY) {
                parsestring = decoder.readString(AttributeId.ATTRIB_CONTENT);
                decoder.closeElement(elemId);
            }
            if (parsestring.Length == 0 && !dynamic)
                throw new CORE.LowlevelError($"Missing <body> subtag in <pcode>: {getSource()}");
        }

        public InjectPayloadSleigh(string src, string nm, InjectPayload.InjectionType tp)
            : base(nm, tp)
        {
            source = src;
            tpl = (ConstructTpl)null;
            paramshift = 0;
        }

        ~InjectPayloadSleigh()
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
            setupParameters(con, walker, inputlist, output, source);
            // delayslot and crossbuild directives are not allowed in snippets, so we don't need the DisassemblyCache
            // and we don't need a unique allocation mask
            SleighBuilder builder = new SleighBuilder(walker, (DisassemblyCache)null, con.cacher,
                con.glb.getConstantSpace(),con.glb.getUniqueSpace(),0);
            builder.build(tpl, -1);
            con.cacher.resolveRelatives();
            con.cacher.emit(con.baseaddr, emit);
        }

        internal override void decode(Sla.CORE.Decoder decoder)
        {
            // Restore a raw <pcode> tag.  Used for uponentry, uponreturn
            ElementId elemId = decoder.openElement(ElementId.ELEM_PCODE);
            decodePayloadAttributes(decoder);
            decodePayloadParams(decoder);
            decodeBody(decoder);
            decoder.closeElement(elemId);
        }

        internal override void printTemplate(TextWriter s)
        {
            tpl.saveXml(s, -1);
        }

        protected override string getSource() => source;

        public static void checkParameterRestrictions(InjectContextSleigh con,
            List<InjectParameter> inputlist, List<InjectParameter> output, string source)
        {
            // Verify that the storage locations passed in -con- match the restrictions set for this payload
            if (inputlist.size() != con.inputlist.size())
                throw new CORE.LowlevelError("Injection parameter list has different number of parameters than p-code operation: " + source);
            for (int i = 0; i < inputlist.size(); ++i) {
                uint sz = inputlist[i].getSize();
                if ((sz != 0) && (sz != con.inputlist[i].size))
                    throw new CORE.LowlevelError("P-code input parameter size does not match injection specification: " + source);
            }
            if (output.size() != con.output.size())
                throw new CORE.LowlevelError("Injection output does not match output of p-code operation: " + source);
            for (int i = 0; i < output.size(); ++i) {
                uint sz = output[i].getSize();
                if ((sz != 0) && (sz != con.output[i].size))
                    throw new CORE.LowlevelError("P-code output size does not match injection specification: " + source);
            }
        }

        public static void setupParameters(InjectContextSleigh con, ParserWalkerChange walker,
            List<InjectParameter> inputlist, List<InjectParameter> output, string source)
        {
            // Set-up operands in the parser state so that they pick up storage locations in InjectContext
            checkParameterRestrictions(con, inputlist, output, source);
            ParserContext pos = walker.getParserContext();
            for (int i = 0; i < inputlist.size(); ++i) {
                pos.allocateOperand(inputlist[i].getIndex(), walker);
                VarnodeData data = con.inputlist[i];
                FixedHandle hand = walker.getParentHandle();
                hand.space = data.space;
                hand.offset_offset = data.offset;
                hand.size = data.size;
                hand.offset_space = (AddrSpace)null;
                walker.popOperand();
            }
            for (int i = 0; i < output.size(); ++i) {
                pos.allocateOperand(output[i].getIndex(), walker);
                VarnodeData data = con.output[i];
                FixedHandle hand = walker.getParentHandle();
                hand.space = data.space;
                hand.offset_offset = data.offset;
                hand.size = data.size;
                hand.offset_space = (AddrSpace)null;
                walker.popOperand();
            }
        }
    }
}
