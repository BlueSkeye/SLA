using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class ValueMapSymbol : ValueSymbol
    {
        private List<long> valuetable= new List<long>();
        private bool tableisfilled;

        private void checkTableFill()
        {
            // Check if all possible entries in the table have been filled
            long min = patval.minValue();
            long max = patval.maxValue();
            tableisfilled = (min >= 0) && (max < valuetable.size());
            for (int i = 0; i < valuetable.size(); ++i) {
                if (valuetable[i] == 0xBADBEEF)
                    tableisfilled = false;
            }
        }
        public ValueMapSymbol()
        {
        }
        
        public ValueMapSymbol(string nm,PatternValue pv, List<long> vt) 
            : base(nm, pv)
        {
            valuetable = vt;
            checkTableFill();
        }
        
        public override Constructor resolve(ParserWalker walker)
        {
            if (!tableisfilled) {
                long ind = patval.getValue(walker);
                if ((ind >= valuetable.size()) || (ind < 0) || (valuetable[(int)ind] == 0xBADBEEF)) {
                    TextWriter s = new StringWriter();
                    s.Write(walker.getAddr().getShortcut());
                    walker.getAddr().printRaw(s);
                    s.Write(": No corresponding entry in valuetable");
                    throw new BadDataError(s.ToString());
                }
            }
            return (Constructor)null;
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            // The resolve routine has checked that -ind- must be a valid index
            hand.space = walker.getConstSpace();
            hand.offset_space = (AddrSpace)null; // Not a dynamic value
            hand.offset_offset = (ulong)valuetable[(int)ind];
            hand.size = 0;      // Cannot provide size
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            // ind is already checked to be in range by the resolve routine
            long val = valuetable[(int)ind];
            if (val >= 0)
                s.Write($"0x{val:X}");
            else
                s.Write($"-0x{-val:X}");
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.valuemap_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<valuemap_sym");
            base.saveXmlHeader(s);
            s.WriteLine(">");
            patval.saveXml(s);
            for (int i = 0; i < valuetable.size(); ++i)
                s.WriteLine($"<valuetab val=\"{valuetable[i]}\"/>");
            s.WriteLine("</valuemap_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<valuemap_sym_head");
            base.saveXmlHeader(s);
            s.Write("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            if (!iter.MoveNext()) throw new ApplicationException();
            patval = (PatternValue)PatternExpression.restoreExpression(iter.Current, trans);
            patval.layClaim();
            while (iter.MoveNext()) {
                long val = long.Parse(iter.Current.getAttributeValue("val"));
                valuetable.Add(val);
            }
            checkTableFill();
        }
    }
}
