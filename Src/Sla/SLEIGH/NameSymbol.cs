using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class NameSymbol : ValueSymbol
    {
        private List<string> nametable;
        private bool tableisfilled;

        private void checkTableFill()
        { // Check if all possible entries in the table have been filled
            intb min = patval->minValue();
            intb max = patval->maxValue();
            tableisfilled = (min >= 0) && (max < nametable.size());
            for (uint4 i = 0; i < nametable.size(); ++i)
            {
                if ((nametable[i] == "_") || (nametable[i] == "\t"))
                {
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
            if (!tableisfilled)
            {
                intb ind = patval->getValue(walker);
                if ((ind >= nametable.size()) || (ind < 0) || ((nametable[ind].size() == 1) && (nametable[ind][0] == '\t')))
                {
                    ostringstream s;
                    s << walker.getAddr().getShortcut();
                    walker.getAddr().printRaw(s);
                    s << ": No corresponding entry in nametable";
                    throw BadDataError(s.str());
                }
            }
            return (Constructor*)0;
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            uint4 ind = (uint4)patval->getValue(walker);
            // ind is already checked to be in range by the resolve routine
            s << nametable[ind];
        }

        public override symbol_type getType() => name_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<name_sym";
            SleighSymbol::saveXmlHeader(s);
            s << ">\n";
            patval->saveXml(s);
            for (int4 i = 0; i < nametable.size(); ++i)
            {
                if (nametable[i] == "\t")       // TAB indicates an illegal index
                    s << "<nametab/>\n";        // Emit tag with no name attribute
                else
                    s << "<nametab name=\"" << nametable[i] << "\"/>\n";
            }
            s << "</name_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<name_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            List list = el->getChildren();
            List::const_iterator iter;
            iter = list.begin();
            patval = (PatternValue*)PatternExpression::restoreExpression(*iter, trans);
            patval->layClaim();
            ++iter;
            while (iter != list.end())
            {
                Element subel = *iter;
                if (subel->getNumAttributes() >= 1)
                    nametable.push_back(subel->getAttributeValue("name"));
                else
                    nametable.push_back("\t");      // TAB indicates an illegal index
                ++iter;
            }
            checkTableFill();
        }
    }
}
