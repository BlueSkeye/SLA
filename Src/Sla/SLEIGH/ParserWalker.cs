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

        protected ConstructState point;    // The current node being visited
        protected int4 depth;         // Depth of the current node
        protected int4[] breadcrumb = new int4[32];    // Path of operands from root

        public ParserWalker(ParserContext c)
        {
            const_context = c;
            cross_context = (ParserContext*)0;
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

        public void setOutOfBandState(Constructor ct, int4 index, ConstructState tempstate, ParserWalker otherwalker)
        { // Initialize walker for future calls into getInstructionBytes assuming -ct- is the current position in the walk
            ConstructState pt = otherwalker.point;
            int4 curdepth = otherwalker.depth;
            while (pt.ct != ct)
            {
                if (curdepth <= 0) return;
                curdepth -= 1;
                pt = pt.parent;
            }
            OperandSymbol* sym = ct.getOperand(index);
            int4 i = sym.getOffsetBase();
            // if i<0, i.e. the offset of the operand is constructor relative
            // its possible that the branch corresponding to the operand
            // has not been constructed yet. Context expressions are
            // evaluated BEFORE the constructors branches are created.
            // So we have to construct the offset explicitly.
            if (i < 0)
                tempstate.offset = pt.offset + sym.getRelativeOffset();
            else
                tempstate.offset = pt.resolve[index].offset;

            tempstate.ct = ct;
            tempstate.length = pt.length;
            point = tempstate;
            depth = 0;
            breadcrumb[0] = 0;
        }

        public bool isState() => (point != (ConstructState*)0);

        public void pushOperand(int4 i)
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

        public uint4 getOffset(int4 i)
        {
            if (i < 0) return point.offset;
            ConstructState op = point.resolve[i];
            return op.offset + op.length;
        }

        public Constructor getConstructor() => point.ct;

        public int4 getOperand() => breadcrumb[depth];

        public FixedHandle getParentHandle() => point.hand;

        public FixedHandle getFixedHandle(int4 i) => point.resolve[i].hand;

        public AddrSpace getCurSpace() => const_context.getCurSpace();

        public AddrSpace getConstSpace() => const_context.getConstSpace();

        public Address getAddr()
        {
            if (cross_context != (ParserContext*)0) { return cross_context.getAddr(); }
            return const_context.getAddr();
        }

        public Address getNaddr()
        {
            if (cross_context != (ParserContext*)0) { return cross_context.getNaddr(); }
            return const_context.getNaddr();
        }

        public Address getN2addr()
        {
            if (cross_context != (ParserContext*)0) { return cross_context.getN2addr(); }
            return const_context.getN2addr();
        }

        public Address getRefAddr()
        {
            if (cross_context != (ParserContext*)0) { return cross_context.getRefAddr(); }
            return const_context.getRefAddr();
        }

        public Address getDestAddr()
        {
            if (cross_context != (ParserContext*)0) { return cross_context.getDestAddr(); }
            return const_context.getDestAddr();
        }

        public int4 getLength() => const_context.getLength();

        public uintm getInstructionBytes(int4 byteoff, int4 numbytes)
        {
            return const_context.getInstructionBytes(byteoff, numbytes, point.offset);
        }

        public uintm getContextBytes(int4 byteoff, int4 numbytes)
        {
            return const_context.getContextBytes(byteoff, numbytes);
        }

        public uintm getInstructionBits(int4 startbit, int4 size)
        {
            return const_context.getInstructionBits(startbit, size, point.offset);
        }

        public uintm getContextBits(int4 startbit, int4 size)
        {
            return const_context.getContextBits(startbit, size);
        }
    }
}
