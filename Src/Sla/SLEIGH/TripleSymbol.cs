﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    // This is the central sleigh object
    internal abstract class TripleSymbol : SleighSymbol
    {
        public TripleSymbol()
        {
        }

        public TripleSymbol(string nm)
            : base(nm)
        {
        }
        
        public virtual Constructor? resolve(ParserWalker walker) => (Constructor)null;

        public abstract PatternExpression getPatternExpression();

        public abstract void getFixedHandle(FixedHandle hand, ParserWalker walker);
        
        public virtual int getSize() => 0;

        public abstract void print(TextWriter s, ParserWalker walker);
    
        public virtual void collectLocalValues(List<ulong> results)
        {
        }
    }
}
