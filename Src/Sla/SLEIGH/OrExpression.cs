﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class OrExpression : BinaryExpression
    {
        public OrExpression()
        {
        }
        
        public OrExpression(PatternExpression l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override long getValue(ParserWalker walker)
        {
            long leftval = getLeft().getValue(walker);
            long rightval = getRight().getValue(walker);
            return leftval | rightval;
        }

        public override long getSubValue(List<long> replace,int listpos)
        {
            long leftval = getLeft().getSubValue(replace, listpos); // Must be left first
            long rightval = getRight().getSubValue(replace, listpos);
            return leftval | rightval;
        }

        public override void saveXml(TextWriter s)
        {
            s.WriteLine("<or_exp>");
            base.saveXml(s);
            s.WriteLine("</or_exp>");
        }
    }
}
