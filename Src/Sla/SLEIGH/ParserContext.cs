﻿using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class ParserContext
    {
        //friend class ParserWalker;
        //friend class ParserWalkerChange;
        // Possible states of the ParserContext
        public enum State
        {
            uninitialized = 0,      // Instruction has not been parsed at all
            disassembly = 1,        // Instruction is parsed in preparation for disassembly
            pcode = 2           // Instruction is parsed in preparation for generating p-code
        }
        
        private Translate translate;     // Instruction parser
        private ParserContext.State parsestate;
        private AddrSpace const_space;
        private byte[] buf = new byte[16];      // Buffer of bytes in the instruction stream
        private uint[] context;     // Pointer to local context
        private int contextsize;       // Number of entries in context array
        private ContextCache contcache;   // Interface for getting/setting context
        private List<ContextSet> contextcommit;
        private Address addr;       // Address of start of instruction
        private Address naddr;      // Address of next instruction
        private /*mutable*/ Address n2addr; // Address of instruction after the next
        private Address calladdr;     // For injections, this is the address of the call being overridden
        private List<ConstructState> state = new List<ConstructState>(); // Current resolved instruction
        internal ConstructState base_state;
        private int alloc;         // Number of ConstructState's allocated
        private int delayslot;     // delayslot depth
        
        public ParserContext(ContextCache ccache, Translate trans)
        {
            parsestate = State.uninitialized;
            contcache = ccache;
            translate = trans;
            if (ccache != (ContextCache)null) {
                contextsize = ccache.getDatabase().getContextSize();
                context = new uint[contextsize];
            }
            else {
                contextsize = 0;
                context = null;
            }
        }

        ~ParserContext()
        {
            // if (context != null) delete[] context;
        }

        public byte[] getBuffer() => buf;

        public void initialize(int maxstate, int maxparam, AddrSpace spc)
        {
            const_space = spc;
            state.resize(maxstate);
            state[0].parent = (ConstructState)null;
            for (int i = 0; i < maxstate; ++i)
                state[i].resolve.resize(maxparam);
            base_state = state[0];
        }

        public State getParserState() => parsestate;

        public void setParserState(State st)
        {
            parsestate = st;
        }

        public void deallocateState(ParserWalkerChange walker)
        {
            alloc = 1;
            walker.context = this;
            walker.baseState();
        }

        public void allocateOperand(int i, ParserWalkerChange walker)
        {
            ConstructState opstate = state[alloc++];
            opstate.parent = walker.point;
            opstate.ct = (Constructor)null;
            walker.point.resolve[i] = opstate;
            walker.breadcrumb[walker.depth++] += 1;
            walker.point = opstate;
            walker.breadcrumb[walker.depth] = 0;
        }

        public void setAddr(Address ad)
        {
            addr = ad;
            n2addr = new Address();
        }

        public void setNaddr(Address ad)
        {
            naddr = ad;
        }

        public void setCalladdr(Address ad)
        {
            calladdr = ad;
        }

        public void addCommit(TripleSymbol sym, int num, uint mask, bool flow, ConstructState point)
        {
            contextcommit.Add(new ContextSet() {
                sym = sym,
                point = point,      // This is the current state
                num = num,
                mask = mask,
                value = context[num] & mask,
                flow = flow
            });
        }

        public void clearCommits()
        {
            contextcommit.Clear();
        }

        public void applyCommits()
        {
            if (contextcommit.empty()) return;
            ParserWalker walker = new ParserWalker(this);
            walker.baseState();

            IEnumerator<ContextSet> iter;

            foreach (ContextSet set in contextcommit) {
                TripleSymbol sym = set.sym;
                Address commitaddr;
                if (sym.getType() == SleighSymbol.symbol_type.operand_symbol) {
                    // The value for an OperandSymbol is probabably already
                    // calculated, we just need to find the right
                    // tree node of the state
                    int i = ((OperandSymbol)sym).getIndex();
                    FixedHandle h = set.point.resolve[i].hand;
                    commitaddr = new Address(h.space, h.offset_offset);
                }
                else {
                    FixedHandle hand = new FixedHandle();
                    sym.getFixedHandle(hand, walker);
                    commitaddr = new Address(hand.space, hand.offset_offset);
                }
                if (commitaddr.isConstant()) {
                    // If the symbol handed to globalset was a computed value, the getFixedHandle calculation
                    // will return a value in the constant space. If this is a case, we explicitly convert the
                    // offset into the current address space
                    ulong newoff = AddrSpace.addressToByte(commitaddr.getOffset(), addr.getSpace().getWordSize());
                    commitaddr = new Address(addr.getSpace(), newoff);
                }

                // Commit context change
                if (set.flow)       // The context flows
                    contcache.setContext(commitaddr, set.num, set.mask, set.value);
                else {
                    // Set the context so that is doesn't flow
                    Address nextaddr = commitaddr + 1;
                    if (nextaddr.getOffset() < commitaddr.getOffset())
                        contcache.setContext(commitaddr, set.num, set.mask, set.value);
                    else
                        contcache.setContext(commitaddr, nextaddr, set.num, set.mask, set.value);
                }
            }
        }

        public Address getAddr() => addr;

        public Address getNaddr() => naddr;

        public Address getN2addr()
        {
            if (n2addr.isInvalid()) {
                if (translate == (Translate)null || parsestate == State.uninitialized)
                    throw new LowlevelError("inst_next2 not available in this context");
                int length = translate.instructionLength(naddr);
                n2addr = naddr + length;
            }
            return n2addr;
        }

        public Address getDestAddr() => calladdr;

        public Address getRefAddr() => calladdr;

        public AddrSpace getCurSpace() => addr.getSpace();

        public AddrSpace getConstSpace() => const_space;

        public uint getInstructionBytes(int bytestart, int size, uint off)
        {
            // Get bytes from the instruction stream into a intm (assuming big endian format)
            off += (uint)bytestart;
            if (off >= 16)
                throw new BadDataError("Instruction is using more than 16 bytes");
            // byte* ptr = buf + off;
            uint res = 0;
            for (int i = 0; i < size; ++i) {
                res <<= 8;
                res |= buf[off + i];
            }
            return res;
        }

        public uint getContextBytes(int bytestart, int size)
        {
            // Get bytes from context into a uint
            int intstart = bytestart / sizeof(uint);
            uint res = context[intstart];
            int byteOffset = bytestart % sizeof(uint);
            int unusedBytes = sizeof(uint) - size;
            res <<= byteOffset * 8;
            res >>= unusedBytes * 8;
            int remaining = size - sizeof(uint) + byteOffset;
            if ((remaining > 0) && (++intstart < contextsize)) {
                // If we extend beyond boundary of a single uint
                uint res2 = context[intstart];
                unusedBytes = sizeof(uint) - remaining;
                res2 >>= unusedBytes * 8;
                res |= res2;
            }
            return res;
        }

        public uint getInstructionBits(int startbit, int size, uint off)
        {
            off += (startbit / 8);
            if (off >= 16)
                throw new BadDataError("Instruction is using more than 16 bytes");
            startbit = startbit % 8;
            int bytesize = (startbit + size - 1) / 8 + 1;
            uint res = 0;
            for (int i = 0; i < bytesize; ++i) {
                res <<= 8;
                res |= buf[off + i];
            }
            // Move starting bit to highest position
            res <<= 8 * (sizeof(uint) - bytesize) + startbit;
            // Shift to bottom of intm
            res >>= 8 * sizeof(uint) - size;
            return res;
        }

        public uint getContextBits(int startbit, int size)
        {
            int intstart = startbit / (8 * sizeof(uint));
            uint res = context[intstart]; // Get intm containing highest bit
            int bitOffset = startbit % (8 * sizeof(uint));
            int unusedBits = 8 * sizeof(uint) - size;
            res <<= bitOffset;  // Shift startbit to highest position
            res >>= unusedBits;
            int remaining = size - 8 * sizeof(uint) + bitOffset;
            if ((remaining > 0) && (++intstart < contextsize)) {
                uint res2 = context[intstart];
                unusedBits = 8 * sizeof(uint) - remaining;
                res2 >>= unusedBits;
                res |= res2;
            }
            return res;
        }

        public void setContextWord(int i, uint val, uint mask)
        {
            context[i] = (context[i] & (~mask)) | (mask & val);
        }

        public void loadContext()
        {
            contcache.getContext(addr, context);
        }

        public int getLength() => base_state.length;

        public void setDelaySlot(int val)
        {
            delayslot = val;
        }

        public int getDelaySlot() => delayslot;
    }
}
