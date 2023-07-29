using Sla.CORE;
using Sla.DECCORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class ValueSymbol : FamilySymbol
    {
        protected PatternValue patval;
        
        public ValueSymbol()
        {
            patval = (PatternValue*)0;
        }

        public ValueSymbol(string nm,PatternValue pv)
            : base(nm)
        {
            (patval = pv).layClaim();
        }

        ~ValueSymbol()
        {
            if (patval != (PatternValue*)0)
                PatternExpression::release(patval);
        }

        public override PatternValue getPatternValue() => patval;

        public override PatternExpression getPatternExpression() => patval;

        public override void getFixedHandle(FixedHandle hand, ParserWalker walker)
        {
            hand.space = walker.getConstSpace();
            hand.offset_space = (AddrSpace*)0;
            hand.offset_offset = (uintb)patval.getValue(walker);
            hand.size = 0;      // Cannot provide size
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            intb val = patval.getValue(walker);
            if (val >= 0)
                s << "0x" << hex << val;
            else
                s << "-0x" << hex << -val;
        }

        public override symbol_type getType() => value_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<value_sym";
            SleighSymbol::saveXmlHeader(s);
            s << ">\n";
            patval.saveXml(s);
            s << "</value_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<value_sym_head";
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
        }
    }
}
