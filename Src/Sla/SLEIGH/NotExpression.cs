using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class NotExpression : UnaryExpression
    {
        public NotExpression()
        {
        }
        
        public NotExpression(PatternExpression u)
            : base(u)
        {
        }
        
        public override long getValue(ParserWalker walker)
        {
            long val = getUnary().getValue(walker);
            return ~val;
        }

        public override long getSubValue(List<long> replace,int listpos)
        {
            long val = getUnary().getSubValue(replace, listpos);
            return ~val;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<not_exp>\n";
            UnaryExpression::saveXml(s);
            s << "</not_exp>\n";
        }
    }
}
