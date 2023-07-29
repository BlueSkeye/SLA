using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class XorExpression : BinaryExpression
    {
        public XorExpression()
        {
        }
        
        public XorExpression(PatternExpression l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override long getValue(ParserWalker walker)
        {
            long leftval = getLeft().getValue(walker);
            long rightval = getRight().getValue(walker);
            return leftval ^ rightval;
        }

        public override long getSubValue(List<long> replace,int listpos)
        {
            long leftval = getLeft().getSubValue(replace, listpos); // Must be left first
            long rightval = getRight().getSubValue(replace, listpos);
            return leftval ^ rightval;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<xor_exp>\n";
            BinaryExpression::saveXml(s);
            s << "</xor_exp>\n";
        }
    }
}
