using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.EXTRA
{
    internal class InjectPayloadSleigh : InjectPayload
    {
        // friend class PcodeInjectLibrarySleigh;
        private ConstructTpl tpl;
        private string parsestring;
        private string source;

        ///< Parse the <body> tag
        /// The content is read as raw p-code source
        /// \param decoder is the XML stream decoder
        protected void decodeBody(Decoder decoder)
        {
            uint elemId = decoder.openElement();       // Tag may not be present
            if (elemId == ELEM_BODY)
            {
                parsestring = decoder.readString(ATTRIB_CONTENT);
                decoder.closeElement(elemId);
            }
            if (parsestring.size() == 0 && (!dynamic))
                throw new LowlevelError("Missing <body> subtag in <pcode>: " + getSource());
        }

        public InjectPayloadSleigh(string src, string nm, int tp)
            : base(nm, tp)
        {
            source = src;
            tpl = (ConstructTpl*)0;
            paramshift = 0;
        }

        ~InjectPayloadSleigh()
        {
            if (tpl != (ConstructTpl*)0)
                delete tpl;
        }

        public override void inject(InjectContext context, PcodeEmit emit)
        {
            InjectContextSleigh & con((InjectContextSleigh &)context);

            con.cacher.clear();

            con.pos.setAddr(con.baseaddr);
            con.pos.setNaddr(con.nextaddr);
            con.pos.setCalladdr(con.calladdr);

            ParserWalkerChange walker(con.pos);
            con.pos.deallocateState(walker);
            setupParameters(con, walker, inputlist, output, source);
            // delayslot and crossbuild directives are not allowed in snippets, so we don't need the DisassemblyCache
            // and we don't need a unique allocation mask
            SleighBuilder builder(&walker,(DisassemblyCache*)0,&con.cacher,con.glb.getConstantSpace(),con.glb.getUniqueSpace(),0);
            builder.build(tpl, -1);
            con.cacher.resolveRelatives();
            con.cacher.emit(con.baseaddr, &emit);
        }

        public override void decode(Decoder decoder)
        {
            // Restore a raw <pcode> tag.  Used for uponentry, uponreturn
            uint elemId = decoder.openElement(ELEM_PCODE);
            decodePayloadAttributes(decoder);
            decodePayloadParams(decoder);
            decodeBody(decoder);
            decoder.closeElement(elemId);
        }

        public override void printTemplate(TextWriter s)
        {
            tpl.saveXml(s, -1);
        }

        public override string getSource() => source;

        public static void checkParameterRestrictions(InjectContextSleigh con,
            List<InjectParameter> inputlist, List<InjectParameter> output, string source)
        { // Verify that the storage locations passed in -con- match the restrictions set for this payload
            if (inputlist.size() != con.inputlist.size())
                throw new LowlevelError("Injection parameter list has different number of parameters than p-code operation: " + source);
            for (int i = 0; i < inputlist.size(); ++i)
            {
                uint sz = inputlist[i].getSize();
                if ((sz != 0) && (sz != con.inputlist[i].size))
                    throw new LowlevelError("P-code input parameter size does not match injection specification: " + source);
            }
            if (output.size() != con.output.size())
                throw new LowlevelError("Injection output does not match output of p-code operation: " + source);
            for (int i = 0; i < output.size(); ++i)
            {
                uint sz = output[i].getSize();
                if ((sz != 0) && (sz != con.output[i].size))
                    throw new LowlevelError("P-code output size does not match injection specification: " + source);
            }
        }

        public static void setupParameters(InjectContextSleigh con, ParserWalkerChange walker,
            List<InjectParameter> inputlist, List<InjectParameter> output, string source)
        { // Set-up operands in the parser state so that they pick up storage locations in InjectContext
            checkParameterRestrictions(con, inputlist, output, source);
            ParserContext* pos = walker.getParserContext();
            for (int i = 0; i < inputlist.size(); ++i)
            {
                pos.allocateOperand(inputlist[i].getIndex(), walker);
                VarnodeData & data(con.inputlist[i]);
                FixedHandle & hand(walker.getParentHandle());
                hand.space = data.space;
                hand.offset_offset = data.offset;
                hand.size = data.size;
                hand.offset_space = (AddrSpace*)0;
                walker.popOperand();
            }
            for (int i = 0; i < output.size(); ++i)
            {
                pos.allocateOperand(output[i].getIndex(), walker);
                VarnodeData & data(con.output[i]);
                FixedHandle & hand(walker.getParentHandle());
                hand.space = data.space;
                hand.offset_offset = data.offset;
                hand.size = data.size;
                hand.offset_space = (AddrSpace*)0;
                walker.popOperand();
            }
        }
    }
}
