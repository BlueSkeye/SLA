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
    internal class VarnodeListSymbol : ValueSymbol
    {
        private List<VarnodeSymbol> varnode_table;
        private bool tableisfilled;

        private void checkTableFill()
        {
            long min = patval.minValue();
            long max = patval.maxValue();
            tableisfilled = (min >= 0) && (max < varnode_table.size());
            for (uint i = 0; i < varnode_table.size(); ++i)
            {
                if (varnode_table[i] == (VarnodeSymbol*)0)
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
                varnode_table.Add((VarnodeSymbol*)vt[i]);
            checkTableFill();
        }

        public override Constructor resolve(ParserWalker walker)
        {
            if (!tableisfilled)
            {
                long ind = patval.getValue(walker);
                if ((ind < 0) || (ind >= varnode_table.size()) || (varnode_table[ind] == (VarnodeSymbol*)0))
                {
                    ostringstream s;
                    s << walker.getAddr().getShortcut();
                    walker.getAddr().printRaw(s);
                    s << ": No corresponding entry in varnode list";
                    throw BadDataError(s.str());
                }
            }
            return (Constructor)null;
        }

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            // The resolve routine has checked that -ind- must be a valid index
            VarnodeData fix = varnode_table[ind].getFixedVarnode();
            hand.space = fix.space;
            hand.offset_space = (AddrSpace)null; // Not a dynamic value
            hand.offset_offset = fix.offset;
            hand.size = fix.size;
        }

        public override int getSize()
        {
            for (int i = 0; i < varnode_table.size(); ++i)
            {
                VarnodeSymbol* vnsym = varnode_table[i]; // Assume all are same size
                if (vnsym != (VarnodeSymbol*)0)
                    return vnsym.getSize();
            }
            throw new SleighError("No register attached to: " + getName());
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            uint ind = (uint)patval.getValue(walker);
            if (ind >= varnode_table.size())
                throw new SleighError("Value out of range for varnode table");
            s << varnode_table[ind].getName();
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.varnodelist_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<varlist_sym";
            SleighSymbol::saveXmlHeader(s);
            s << ">\n";
            patval.saveXml(s);
            for (int i = 0; i < varnode_table.size(); ++i)
            {
                if (varnode_table[i] == (VarnodeSymbol*)0)
                    s << "<null/>\n";
                else
                    s << "<var id=\"0x" << hex << varnode_table[i].getId() << "\"/>\n";
            }
            s << "</varlist_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<varlist_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
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
                if (subel.getName() == "var")
                {
                    uint id;
                    istringstream s = new istringstream(subel.getAttributeValue("id"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> id;
                    varnode_table.Add((VarnodeSymbol*)trans.findSymbol(id));
                }
                else
                    varnode_table.Add((VarnodeSymbol*)0);
                ++iter;
            }
            checkTableFill();
        }
    }
}
