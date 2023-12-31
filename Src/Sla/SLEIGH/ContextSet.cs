﻿
namespace Sla.SLEIGH
{
    internal struct ContextSet
    {
        internal TripleSymbol sym;      // Resolves to address where setting takes effect
        internal ConstructState point;  // Point at which context set was made
        internal int num;           // Number of context word affected
        internal uint mask;         // Bits within word affected
        internal uint value;            // New setting for bits
        internal bool flow;			// Does the new context flow from its set point
    }
}
