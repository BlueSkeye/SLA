﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class SubExpression : BinaryExpression
    {
        public SubExpression()
        {
        }

        public SubExpression(PatternExpression l, PatternExpression r)
            : base(l, r)
        {
        }

        public override intb getValue(ParserWalker walker)
        {
            intb leftval = getLeft()->getValue(walker);
            intb rightval = getRight()->getValue(walker);
            return leftval - rightval;
        }

        public override intb getSubValue(List<intb> replace,int4 listpos)
        {
            intb leftval = getLeft()->getSubValue(replace, listpos); // Must be left first
            intb rightval = getRight()->getSubValue(replace, listpos);
            return leftval - rightval;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<sub_exp>\n";
            BinaryExpression::saveXml(s);
            s << "</sub_exp>\n";
        }
    }
}