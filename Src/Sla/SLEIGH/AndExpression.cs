using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class AndExpression : BinaryExpression
    {
        public AndExpression()
        {
        }
        
        public AndExpression(PatternExpression l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override intb getValue(ParserWalker walker)
        {
            intb leftval = getLeft().getValue(walker);
            intb rightval = getRight().getValue(walker);
            return leftval & rightval;
        }

        public override intb getSubValue(List<intb> replace,int4 listpos)
        {
            intb leftval = getLeft().getValue(walker);
            intb rightval = getRight().getValue(walker);
            return leftval & rightval;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<and_exp>\n";
            BinaryExpression::saveXml(s);
            s << "</and_exp>\n";
        }
    }
}
