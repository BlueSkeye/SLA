﻿using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class Pattern
    { 
        ~Pattern()
        {
        }

        public abstract Pattern simplifyClone();

        public abstract void shiftInstruction(int sa);

        public abstract Pattern doOr(Pattern b, int sa);

        public abstract Pattern doAnd(Pattern b, int sa);

        public abstract Pattern commonSubPattern(Pattern b, int sa);

        // Does this pattern match context
        public abstract bool isMatch(ParserWalker walker);

        public abstract int numDisjoint();

        public abstract DisjointPattern getDisjoint(int i);

        public abstract bool alwaysTrue();

        public abstract bool alwaysFalse();

        public abstract bool alwaysInstructionTrue();

        public abstract void saveXml(TextWriter s);

        public abstract void restoreXml(Element el);
    }
}
