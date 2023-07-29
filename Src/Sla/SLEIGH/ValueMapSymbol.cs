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
    internal class ValueMapSymbol : ValueSymbol
    {
        private List<intb> valuetable;
        private bool tableisfilled;

        private void checkTableFill()
        { // Check if all possible entries in the table have been filled
            intb min = patval->minValue();
            intb max = patval->maxValue();
            tableisfilled = (min >= 0) && (max < valuetable.size());
            for (uint4 i = 0; i < valuetable.size(); ++i)
            {
                if (valuetable[i] == 0xBADBEEF)
                    tableisfilled = false;
            }
        }
        public ValueMapSymbol()
        {
        }
        
        public ValueMapSymbol(string nm,PatternValue pv, List<intb> vt) 
            : base(nm, pv)
        {
            valuetable = vt;
            checkTableFill();
        }
        
        public override Constructor resolve(ParserWalker walker)
        {
            if (!tableisfilled)
            {
                intb ind = patval->getValue(walker);
                if ((ind >= valuetable.size()) || (ind < 0) || (valuetable[ind] == 0xBADBEEF))
                {
                    ostringstream s;
                    s << walker.getAddr().getShortcut();
                    walker.getAddr().printRaw(s);
                    s << ": No corresponding entry in valuetable";
                    throw BadDataError(s.str());
                }
            }
            return (Constructor*)0;
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            uint4 ind = (uint4)patval->getValue(walker);
            // The resolve routine has checked that -ind- must be a valid index
            hand.space = walker.getConstSpace();
            hand.offset_space = (AddrSpace*)0; // Not a dynamic value
            hand.offset_offset = (uintb)valuetable[ind];
            hand.size = 0;      // Cannot provide size
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            uint4 ind = (uint4)patval->getValue(walker);
            // ind is already checked to be in range by the resolve routine
            intb val = valuetable[ind];
            if (val >= 0)
                s << "0x" << hex << val;
            else
                s << "-0x" << hex << -val;
        }

        public override symbol_type getType() => valuemap_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<valuemap_sym";
            SleighSymbol::saveXmlHeader(s);
            s << ">\n";
            patval->saveXml(s);
            for (uint4 i = 0; i < valuetable.size(); ++i)
                s << "<valuetab val=\"" << dec << valuetable[i] << "\"/>\n";
            s << "</valuemap_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<valuemap_sym_head";
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
                istringstream s((* iter)->getAttributeValue("val"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                intb val;
                s >> val;
                valuetable.push_back(val);
                ++iter;
            }
            checkTableFill();
        }
    }
}
