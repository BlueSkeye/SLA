using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class MinusExpression : UnaryExpression
    {
        public MinusExpression()
        {
        }
        
        public MinusExpression(PatternExpression u)
            : base(u)
        {
        }
        
        public override intb getValue(ParserWalker walker)
        {
            intb val = getUnary()->getValue(walker);
            return -val;
        }

        public override intb getSubValue(List<intb> replace,int4 listpos)
        {
            intb val = getUnary()->getSubValue(replace, listpos);
            return -val;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<minus_exp>\n";
            UnaryExpression::saveXml(s);
            s << "</minus_exp>\n";
        }
    }
}
