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
        private int4 parsestate;
        private AddrSpace const_space;
        private uint1[] buf = new uint1[16];      // Buffer of bytes in the instruction stream
        private uintm context;     // Pointer to local context
        private int4 contextsize;       // Number of entries in context array
        private ContextCache contcache;   // Interface for getting/setting context
        private List<ContextSet> contextcommit;
        private Address addr;       // Address of start of instruction
        private Address naddr;      // Address of next instruction
        private /*mutable*/ Address n2addr; // Address of instruction after the next
        private Address calladdr;     // For injections, this is the address of the call being overridden
        private List<ConstructState> state; // Current resolved instruction
        private ConstructState base_state;
        private int4 alloc;         // Number of ConstructState's allocated
        private int4 delayslot;     // delayslot depth
        
        public ParserContext(ContextCache ccache, Translate trans)
        {
            parsestate = uninitialized;
            contcache = ccache;
            translate = trans;
            if (ccache != (ContextCache*)0)
            {
                contextsize = ccache->getDatabase()->getContextSize();
                context = new uintm[contextsize];
            }
            else
            {
                contextsize = 0;
                context = (uintm*)0;
            }
        }

        ~ParserContext()
        {
            if (context != (uintm*)0) delete[] context;
        }

        public uint1 getBuffer() => buf;

        public void initialize(int4 maxstate, int4 maxparam, AddrSpace spc)
        {
            const_space = spc;
            state.resize(maxstate);
            state[0].parent = (ConstructState*)0;
            for (int4 i = 0; i < maxstate; ++i)
                state[i].resolve.resize(maxparam);
            base_state = &state[0];
        }

        public int4 getParserState() => parsestate;

        public void setParserState(int4 st)
        {
            parsestate = st;
        }

        public void deallocateState(ParserWalkerChange walker)
        {
            alloc = 1;
            walker.context = this;
            walker.baseState();
        }

        public void allocateOperand(int4 i, ParserWalkerChange walker)
        {
            ConstructState* opstate = &state[alloc++];
            opstate->parent = walker.point;
            opstate->ct = (Constructor*)0;
            walker.point->resolve[i] = opstate;
            walker.breadcrumb[walker.depth++] += 1;
            walker.point = opstate;
            walker.breadcrumb[walker.depth] = 0;
        }

        public void setAddr(Address ad)
        {
            addr = ad;
            n2addr = Address();
        }

        public void setNaddr(Address ad)
        {
            naddr = ad;
        }

        public void setCalladdr(Address ad)
        {
            calladdr = ad;
        }

        public void addCommit(TripleSymbol sym, int4 num, uintm mask, bool flow, ConstructState point)
        {
            contextcommit.emplace_back();
            ContextSet & set(contextcommit.back());

            set.sym = sym;
            set.point = point;      // This is the current state
            set.num = num;
            set.mask = mask;
            set.value = context[num] & mask;
            set.flow = flow;
        }

        public void clearCommits()
        {
            contextcommit.clear();
        }

        public void applyCommits()
        {
            if (contextcommit.empty()) return;
            ParserWalker walker(this);
            walker.baseState();

            vector<ContextSet>::iterator iter;

            for (iter = contextcommit.begin(); iter != contextcommit.end(); ++iter)
            {
                TripleSymbol* sym = (*iter).sym;
                Address commitaddr;
                if (sym->getType() == SleighSymbol::operand_symbol)
                {
                    // The value for an OperandSymbol is probabably already
                    // calculated, we just need to find the right
                    // tree node of the state
                    int4 i = ((OperandSymbol*)sym)->getIndex();
                    FixedHandle & h((*iter).point->resolve[i]->hand);
                    commitaddr = Address(h.space, h.offset_offset);
                }
                else
                {
                    FixedHandle hand;
                    sym->getFixedHandle(hand, walker);
                    commitaddr = Address(hand.space, hand.offset_offset);
                }
                if (commitaddr.isConstant())
                {
                    // If the symbol handed to globalset was a computed value, the getFixedHandle calculation
                    // will return a value in the constant space. If this is a case, we explicitly convert the
                    // offset into the current address space
                    uintb newoff = AddrSpace::addressToByte(commitaddr.getOffset(), addr.getSpace()->getWordSize());
                    commitaddr = Address(addr.getSpace(), newoff);
                }

                // Commit context change
                if ((*iter).flow)       // The context flows
                    contcache->setContext(commitaddr, (*iter).num, (*iter).mask, (*iter).value);
                else
                {  // Set the context so that is doesn't flow
                    Address nextaddr = commitaddr + 1;
                    if (nextaddr.getOffset() < commitaddr.getOffset())
                        contcache->setContext(commitaddr, (*iter).num, (*iter).mask, (*iter).value);
                    else
                        contcache->setContext(commitaddr, nextaddr, (*iter).num, (*iter).mask, (*iter).value);
                }
            }
        }

        public Address getAddr() => addr;

        public Address getNaddr() => naddr;

        public Address getN2addr()
        {
            if (n2addr.isInvalid())
            {
                if (translate == (Translate*)0 || parsestate == uninitialized)
                    throw new LowlevelError("inst_next2 not available in this context");
                int4 length = translate->instructionLength(naddr);
                n2addr = naddr + length;
            }
            return n2addr;
        }

        public Address getDestAddr() => calladdr;

        public Address getRefAddr() => calladdr;

        public AddrSpace getCurSpace() => addr.getSpace();

        public AddrSpace getConstSpace() => const_space;

        public uintm getInstructionBytes(int4 byteoff, int4 numbytes, uint4 off)
        {               // Get bytes from the instruction stream into a intm
                        // (assuming big endian format)
            off += bytestart;
            if (off >= 16)
                throw BadDataError("Instruction is using more than 16 bytes");
            const uint1* ptr = buf + off;
            uintm res = 0;
            for (int4 i = 0; i < size; ++i)
            {
                res <<= 8;
                res |= ptr[i];
            }
            return res;
        }

        public uintm getContextBytes(int4 byteoff, int4 numbytes)
        {               // Get bytes from context into a uintm
            int4 intstart = bytestart / sizeof(uintm);
            uintm res = context[intstart];
            int4 byteOffset = bytestart % sizeof(uintm);
            int4 unusedBytes = sizeof(uintm) - size;
            res <<= byteOffset * 8;
            res >>= unusedBytes * 8;
            int4 remaining = size - sizeof(uintm) + byteOffset;
            if ((remaining > 0) && (++intstart < contextsize))
            { // If we extend beyond boundary of a single uintm
                uintm res2 = context[intstart];
                unusedBytes = sizeof(uintm) - remaining;
                res2 >>= unusedBytes * 8;
                res |= res2;
            }
            return res;
        }

        public uintm getInstructionBits(int4 startbit, int4 size, uint4 off)
        {
            off += (startbit / 8);
            if (off >= 16)
                throw BadDataError("Instruction is using more than 16 bytes");
            const uint1* ptr = buf + off;
            startbit = startbit % 8;
            int4 bytesize = (startbit + size - 1) / 8 + 1;
            uintm res = 0;
            for (int4 i = 0; i < bytesize; ++i)
            {
                res <<= 8;
                res |= ptr[i];
            }
            res <<= 8 * (sizeof(uintm) - bytesize) + startbit; // Move starting bit to highest position
            res >>= 8 * sizeof(uintm) - size;   // Shift to bottom of intm
            return res;
        }

        public uintm getContextBits(int4 startbit, int4 size)
        {
            int4 intstart = startbit / (8 * sizeof(uintm));
            uintm res = context[intstart]; // Get intm containing highest bit
            int4 bitOffset = startbit % (8 * sizeof(uintm));
            int4 unusedBits = 8 * sizeof(uintm) - size;
            res <<= bitOffset;  // Shift startbit to highest position
            res >>= unusedBits;
            int4 remaining = size - 8 * sizeof(uintm) + bitOffset;
            if ((remaining > 0) && (++intstart < contextsize))
            {
                uintm res2 = context[intstart];
                unusedBits = 8 * sizeof(uintm) - remaining;
                res2 >>= unusedBits;
                res |= res2;
            }
            return res;
        }

        public void setContextWord(int4 i, uintm val, uintm mask)
        {
            context[i] = (context[i] & (~mask)) | (mask & val);
        }

        public void loadContext()
        {
            contcache->getContext(addr, context);
        }

        public int4 getLength() => base_state->length;

        public void setDelaySlot(int4 val)
        {
            delayslot = val;
        }

        public int4 getDelaySlot() => delayslot;
    }
}