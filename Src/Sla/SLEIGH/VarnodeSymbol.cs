using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Sla.DECCORE.FuncCallSpecs;
using static Sla.SLEIGH.SleighSymbol;

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
            return new VarnodeTpl(ConstTpl(fix.space), ConstTpl(ConstTpl::real, fix.offset), ConstTpl(ConstTpl::real, fix.size));
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = fix.space;
            hand.offset_space = (AddrSpace*)0; // Not a dynamic symbol
            hand.offset_offset = fix.offset;
            hand.size = fix.size;
        }

        public override int getSize() => fix.size;

        public override void print(TextWriter s, ParserWalker walker)
        {
            s << getName();
        }

        public override void collectLocalValues(List<ulong> results)
        {
            if (fix.space.getType() == IPTR_INTERNAL)
                results.push_back(fix.offset);
        }

        public override symbol_type getType() => varnode_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<varnode_sym";
            SleighSymbol::saveXmlHeader(s);
            s << " space=\"" << fix.space.getName() << "\"";
            s << " offset=\"0x" << hex << fix.offset << "\"";
            s << " size=\"" << dec << fix.size << "\"";
            s << ">\n";
            PatternlessSymbol::saveXml(s);
            s << "</varnode_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<varnode_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            fix.space = trans.getSpaceByName(el.getAttributeValue("space"));
            {
                istringstream s(el.getAttributeValue("offset"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> fix.offset;
            }
            {
                istringstream s(el.getAttributeValue("size"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> fix.size;
            }
            // PatternlessSymbol does not need restoring
        }
    }
}
