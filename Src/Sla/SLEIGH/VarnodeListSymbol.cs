using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class VarnodeListSymbol : ValueSymbol
    {
        private List<VarnodeSymbol> varnode_table = new List<VarnodeSymbol>();
        private bool tableisfilled;

        private void checkTableFill()
        {
            long min = patval.minValue();
            long max = patval.maxValue();
            tableisfilled = (min >= 0) && (max < varnode_table.size());
            for (int i = 0; i < varnode_table.size(); ++i) {
                if (varnode_table[i] == (VarnodeSymbol)null)
                    tableisfilled = false;
            }
        }

        public VarnodeListSymbol()
        {
        }

        public VarnodeListSymbol(string nm,PatternValue pv, List<SleighSymbol> vt)
            : base(nm, pv)
        {
            for (int i = 0; i < vt.size(); ++i)
                varnode_table.Add((VarnodeSymbol)vt[i]);
            checkTableFill();
        }

        public override Constructor resolve(ParserWalker walker)
        {
            if (!tableisfilled) {
                long ind = patval.getValue(walker);
                if (   (ind < 0)
                    || (ind >= varnode_table.size())
                    || (varnode_table[(int)ind] == (VarnodeSymbol)null))
                {
                    TextWriter s = new StringWriter();
                    s.Write(walker.getAddr().getShortcut());
                    walker.getAddr().printRaw(s);
                    s.Write(": No corresponding entry in varnode list");
                    throw new BadDataError(s.ToString());
                }
            }
            return (Constructor)null;
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            // The resolve routine has checked that -ind- must be a valid index
            VarnodeData fix = varnode_table[(int)ind].getFixedVarnode();
            hand.space = fix.space;
            hand.offset_space = (AddrSpace)null; // Not a dynamic value
            hand.offset_offset = fix.offset;
            hand.size = fix.size;
        }

        public override int getSize()
        {
            for (int i = 0; i < varnode_table.size(); ++i) {
                VarnodeSymbol? vnsym = varnode_table[i]; // Assume all are same size
                if (vnsym != (VarnodeSymbol)null)
                    return vnsym.getSize();
            }
            throw new SleighError("No register attached to: " + getName());
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            if (ind >= varnode_table.size())
                throw new SleighError("Value out of range for varnode table");
            s.Write(varnode_table[(int)ind].getName());
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.varnodelist_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<varlist_sym");
            base.saveXmlHeader(s);
            s.WriteLine(">");
            patval.saveXml(s);
            for (int i = 0; i < varnode_table.size(); ++i) {
                if (varnode_table[i] == (VarnodeSymbol)null)
                    s.WriteLine("<null/>");
                else
                    s.Write($"<var id=\"0x{varnode_table[i].getId():X}\"/>");
            }
            s.WriteLine("</varlist_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<varlist_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new ApplicationException();
            patval = (PatternValue)PatternExpression.restoreExpression(iter.Current, trans);
            patval.layClaim();
            while (iter.MoveNext()) {
                Element subel = iter.Current;
                if (subel.getName() == "var") {
                    uint id;
                    id = uint.Parse(subel.getAttributeValue("id"));
                    varnode_table.Add((VarnodeSymbol)trans.findSymbol(id));
                }
                else
                    varnode_table.Add((VarnodeSymbol)null);
            }
            checkTableFill();
        }
    }
}
