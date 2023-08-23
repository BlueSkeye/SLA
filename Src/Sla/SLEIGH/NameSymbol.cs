using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class NameSymbol : ValueSymbol
    {
        private List<string> nametable;
        private bool tableisfilled;

        private void checkTableFill()
        {
            // Check if all possible entries in the table have been filled
            long min = patval.minValue();
            long max = patval.maxValue();
            tableisfilled = (min >= 0) && (max < nametable.size());
            for (int i = 0; i < nametable.size(); ++i) {
                if ((nametable[i] == "_") || (nametable[i] == "\t")) {
                    nametable[i] = "\t";        // TAB indicates illegal index
                    tableisfilled = false;
                }
            }
        }

        public NameSymbol()
        {
        }

        public NameSymbol(string nm,PatternValue pv, List<string> nt)
            : base(nm, pv)
        {
            nametable = nt;
            checkTableFill();
        }

        public override Constructor resolve(ParserWalker walker)
        {
            if (!tableisfilled) {
                long ind = patval.getValue(walker);
                if ((ind >= nametable.size()) || (ind < 0) || ((nametable[ind].size() == 1) && (nametable[ind][0] == '\t')))
                {
                    TextWriter s = new StringWriter();
                    s.Write(walker.getAddr().getShortcut());
                    walker.getAddr().printRaw(s);
                    s.Write(": No corresponding entry in nametable");
                    throw new BadDataError(s.ToString());
                }
            }
            return (Constructor)null;
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            // ind is already checked to be in range by the resolve routine
            s.Write(nametable[(int)ind]);
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.name_symbol;

        public override void saveXml(TextWriter s)
        {
            s.Write("<name_sym");
            base.saveXmlHeader(s);
            s.WriteLine(">");
            patval.saveXml(s);
            for (int i = 0; i < nametable.size(); ++i) {
                if (nametable[i] == "\t")       // TAB indicates an illegal index
                    s.WriteLine("<nametab/>");        // Emit tag with no name attribute
                else
                    s.WriteLine($"<nametab name=\"{nametable[i]}\"/>");
            }
            s.WriteLine("</name_sym>");
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s.Write("<name_sym_head");
            base.saveXmlHeader(s);
            s.WriteLine("/>");
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            patval = (PatternValue*)PatternExpression::restoreExpression(*iter, trans);
            patval.layClaim();
            ++iter;
            while (iter != list.end())
            {
                Element subel = *iter;
                if (subel.getNumAttributes() >= 1)
                    nametable.Add(subel.getAttributeValue("name"));
                else
                    nametable.Add("\t");      // TAB indicates an illegal index
                ++iter;
            }
            checkTableFill();
        }
    }
}
