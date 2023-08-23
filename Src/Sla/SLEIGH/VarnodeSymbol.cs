using Sla.CORE;
using Sla.SLEIGH;

namespace Sla.SLEIGH
{
    internal class VarnodeSymbol : PatternlessSymbol
    {
        // A global varnode
        private VarnodeData fix;
        private bool context_bits;
        
        public VarnodeSymbol()
        {
        }

        public VarnodeSymbol(string nm, AddrSpace @base,ulong offset, int size)
            : base(nm)
        {
            fix.space = @base;
            fix.offset = offset;
            fix.size = size;
            context_bits = false;
        }

        public void markAsContext()
        {
            context_bits = true;
        }
        public VarnodeData getFixedVarnode() => fix;

        public override VarnodeTpl getVarnode()
        {
            return new VarnodeTpl(new ConstTpl(fix.space), new ConstTpl(ConstTpl.const_type.real, fix.offset),
                new ConstTpl(ConstTpl.const_type.real, fix.size));
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = fix.space;
            hand.offset_space = (AddrSpace)null; // Not a dynamic symbol
            hand.offset_offset = fix.offset;
            hand.size = fix.size;
        }

        public override int getSize() => fix.size;

        public override void print(TextWriter s, ParserWalker walker)
        {
            s.Write(getName());
        }

        public override void collectLocalValues(List<ulong> results)
        {
            if (fix.space.getType() == spacetype.IPTR_INTERNAL)
                results.Add(fix.offset);
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.varnode_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<varnode_sym");
            base.saveXmlHeader(s);
            s.WriteLine($" space=\"{fix.space.getName()}\" offset=\"0x{fix.offset:X}\" size=\"{fix.size}\">");
            base.saveXml(s);
            s.WriteLine("</varnode_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<varnode_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            fix.space = trans.getSpaceByName(el.getAttributeValue("space"));
            fix.offset = ulong.Parse(el.getAttributeValue("offset"));
            fix.size = uint.Parse(el.getAttributeValue("size"));
            // PatternlessSymbol does not need restoring
        }
    }
}
