using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ParserWalker
    {
        // A class for walking the ParserContext
        private readonly ParserContext const_context;
        private readonly ParserContext cross_context;

        internal ConstructState point;    // The current node being visited
        protected int depth;         // Depth of the current node
        internal int[] breadcrumb = new int[32];    // Path of operands from root

        public ParserWalker(ParserContext c)
        {
            const_context = c;
            cross_context = (ParserContext)null;
        }

        public ParserWalker(ParserContext c, ParserContext cross)
        {
            const_context = c;
            cross_context = cross;
        }

        public virtual ParserContext getParserContext() => const_context;

        public void baseState()
        {
            point = const_context.base_state;
            depth = 0;
            breadcrumb[0] = 0;
        }

        public void setOutOfBandState(Constructor ct, int index, ConstructState tempstate,
            ParserWalker otherwalker)
        {
            // Initialize walker for future calls into getInstructionBytes assuming -ct- is the current position in the walk
            ConstructState pt = otherwalker.point;
            int curdepth = otherwalker.depth;
            while (pt.ct != ct) {
                if (curdepth <= 0) return;
                curdepth -= 1;
                pt = pt.parent;
            }
            OperandSymbol sym = ct.getOperand(index);
            int i = sym.getOffsetBase();
            // if i<0, i.e. the offset of the operand is constructor relative
            // its possible that the branch corresponding to the operand
            // has not been constructed yet. Context expressions are
            // evaluated BEFORE the constructors branches are created.
            // So we have to construct the offset explicitly.
            tempstate.offset = (i < 0)
                ? pt.offset + sym.getRelativeOffset()
                : pt.resolve[index].offset;

            tempstate.ct = ct;
            tempstate.length = pt.length;
            point = tempstate;
            depth = 0;
            breadcrumb[0] = 0;
        }

        public bool isState() => (point != (ConstructState)null);

        public void pushOperand(int i)
        {
            breadcrumb[depth++] = i + 1;
            point = point.resolve[i];
            breadcrumb[depth] = 0;
        }

        public void popOperand()
        {
            point = point.parent;
            depth -= 1;
        }

        public uint getOffset(int i)
        {
            if (i < 0) return point.offset;
            ConstructState op = point.resolve[i];
            return (uint)(op.offset + op.length);
        }

        public Constructor getConstructor() => point.ct;

        public int getOperand() => breadcrumb[depth];

        public FixedHandle getParentHandle() => point.hand;

        public FixedHandle getFixedHandle(int i) => point.resolve[i].hand;

        public AddrSpace getCurSpace() => const_context.getCurSpace();

        public AddrSpace getConstSpace() => const_context.getConstSpace();

        public Address getAddr()
        {
            if (cross_context != (ParserContext)null) { return cross_context.getAddr(); }
            return const_context.getAddr();
        }

        public Address getNaddr()
        {
            if (cross_context != (ParserContext)null) { return cross_context.getNaddr(); }
            return const_context.getNaddr();
        }

        public Address getN2addr()
        {
            if (cross_context != (ParserContext)null) { return cross_context.getN2addr(); }
            return const_context.getN2addr();
        }

        public Address getRefAddr()
        {
            if (cross_context != (ParserContext)null) { return cross_context.getRefAddr(); }
            return const_context.getRefAddr();
        }

        public Address getDestAddr()
        {
            if (cross_context != (ParserContext)null) { return cross_context.getDestAddr(); }
            return const_context.getDestAddr();
        }

        public int getLength() => const_context.getLength();

        public uint getInstructionBytes(int byteoff, int numbytes)
        {
            return const_context.getInstructionBytes(byteoff, numbytes, point.offset);
        }

        public uint getContextBytes(int byteoff, int numbytes)
        {
            return const_context.getContextBytes(byteoff, numbytes);
        }

        public uint getInstructionBits(int startbit, int size)
        {
            return const_context.getInstructionBits(startbit, size, point.offset);
        }

        public uint getContextBits(int startbit, int size)
        {
            return const_context.getContextBits(startbit, size);
        }
    }
}
