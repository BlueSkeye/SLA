﻿
namespace Sla.SLEIGH
{
    internal class ParserWalkerChange : ParserWalker
    {
        // Extension to walker that allows for on the fly modifications to tree
        // friend class ParserContext;
        internal ParserContext context;
        
        public ParserWalkerChange(ParserContext c)
            : base(c)
        {
            context = c;
        }

        public override ParserContext getParserContext() => context;

        public ConstructState getPoint() => point;

        public void setOffset(uint off)
        {
            point.offset = off;
        }

        public void setConstructor(Constructor c)
        {
            point.ct = c;
        }

        public void setCurrentLength(int len)
        {
            point.length = len;
        }

        public void calcCurrentLength(int length, int numopers)
        {
            // Calculate the length of the current constructor
            // state assuming all its operands are constructed
            length += (int)point.offset;    // Convert relative length to absolute length
            for (int i = 0; i < numopers; ++i) {
                ConstructState subpoint = point.resolve[i];
                int sublength = (int)(subpoint.length + subpoint.offset);
                // Since subpoint.offset is an absolute offset
                // (relative to beginning of instruction) sublength
                if (sublength > length) // is absolute and must be compared to absolute length
                    length = sublength;
            }
            point.length = (int)(length - point.offset); // Convert back to relative length
        }
    }
}
