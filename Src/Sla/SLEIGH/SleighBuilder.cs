using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief Build p-code from a pre-parsed instruction
    ///
    /// Through the build() method, \b this walks the parse tree and prepares data
    /// for final emission as p-code.  (The final emitting is done separately through the
    /// PcodeCacher.emit() method).  Generally, only p-code for one instruction is prepared.
    /// But, through the \b delay-slot mechanism, build() may recursively visit
    /// additional instructions.
    internal class SleighBuilder : PcodeBuilder
    {
        private virtual void dump(OpTpl op)
        {               // Dump on op through low-level dump interface
                        // filling in dynamic loads and stores if necessary
            PcodeData* thisop;
            VarnodeData* invars;
            VarnodeData* loadvars;
            VarnodeData* storevars;
            VarnodeTpl* vn,*outvn;
            int isize = op.numInput();
            // First build all the inputs
            invars = cache.allocateVarnodes(isize);
            for (int i = 0; i < isize; ++i)
            {
                vn = op.getIn(i);
                if (vn.isDynamic(*walker))
                {
                    generateLocation(vn, invars[i]); // Input of -op- is really temporary storage
                    PcodeData* load_op = cache.allocateInstruction();
                    load_op.opc = OpCode.CPUI_LOAD;
                    load_op.outvar = invars + i;
                    load_op.isize = 2;
                    loadvars = load_op.invar = cache.allocateVarnodes(2);
                    AddrSpace* spc = generatePointer(vn, loadvars[1]);
                    loadvars[0].space = const_space;
                    loadvars[0].offset = (ulong)(ulong)spc;
                    loadvars[0].size = sizeof(spc);
                    if (vn.getOffset().getSelect() == ConstTpl::v_offset_plus)
                        generatePointerAdd(load_op, vn);
                }
                else
                    generateLocation(vn, invars[i]);
            }
            if ((isize > 0) && (op.getIn(0).isRelative()))
            {
                invars.offset += getLabelBase();
                cache.addLabelRef(invars);
            }
            thisop = cache.allocateInstruction();
            thisop.opc = op.getOpcode();
            thisop.invar = invars;
            thisop.isize = isize;
            outvn = op.getOut();
            if (outvn != (VarnodeTpl*)0)
            {
                if (outvn.isDynamic(*walker))
                {
                    storevars = cache.allocateVarnodes(3);
                    generateLocation(outvn, storevars[2]); // Output of -op- is really temporary storage
                    thisop.outvar = storevars + 2;
                    PcodeData* store_op = cache.allocateInstruction();
                    store_op.opc = OpCode.CPUI_STORE;
                    store_op.isize = 3;
                    // store_op.outvar = (VarnodeData *)0;
                    store_op.invar = storevars;
                    AddrSpace* spc = generatePointer(outvn, storevars[1]); // pointer
                    storevars[0].space = const_space;
                    storevars[0].offset = (ulong)(ulong)spc; // space in which to store
                    storevars[0].size = sizeof(spc);
                    if (outvn.getOffset().getSelect() == ConstTpl::v_offset_plus)
                        generatePointerAdd(store_op, outvn);
                }
                else
                {
                    thisop.outvar = cache.allocateVarnodes(1);
                    generateLocation(outvn, *thisop.outvar);
                }
            }
        }

        private AddrSpace const_space;     ///< The constant address space
        private AddrSpace uniq_space;      ///< The unique address space
        private ulong uniquemask;           ///< Mask of address bits to use to uniquify temporary registers
        private ulong uniqueoffset;         ///< Uniquifier bits for \b this instruction
        private DisassemblyCache discache;     ///< Cache of disassembled instructions
        private PcodeCacher cache;         ///< Cache accumulating p-code data for the instruction

        /// \brief Build a named p-code section of a constructor that contains only implied BUILD directives
        ///
        /// If a named section of a constructor is empty, we still need to walk
        /// through any subtables that might contain p-code in their named sections.
        /// This method treats each subtable operand as an implied \e build directive,
        /// in the otherwise empty section.
        /// \param ct is the matching currently Constructor being built
        /// \param secnum is the particular \e named section number to build
        private void buildEmpty(Constructor ct, int secnum)
        {
            int numops = ct.getNumOperands();

            for (int i = 0; i < numops; ++i)
            {
                SubtableSymbol* sym = (SubtableSymbol*)ct.getOperand(i).getDefiningSymbol();
                if (sym == (SubtableSymbol)null) continue;
                if (sym.getType() !=  SleighSymbol.symbol_type.subtable_symbol) continue;

                walker.pushOperand(i);
                ConstructTpl* construct = walker.getConstructor().getNamedTempl(secnum);
                if (construct == (ConstructTpl)null)
                    buildEmpty(walker.getConstructor(), secnum);
                else
                    build(construct, secnum);
                walker.popOperand();
            }
        }

        /// \brief Generate a concrete VarnodeData object from the given template (VarnodeTpl)
        ///
        /// \param vntpl is the template to reference
        /// \param vn is the object to fill in with concrete values
        private void generateLocation(VarnodeTpl vntpl, VarnodeData vn)
        {
            vn.space = vntpl.getSpace().fixSpace(*walker);
            vn.size = vntpl.getSize().fix(*walker);
            if (vn.space == const_space)
                vn.offset = vntpl.getOffset().fix(*walker) & Globals.calc_mask(vn.size);
            else if (vn.space == uniq_space)
            {
                vn.offset = vntpl.getOffset().fix(*walker);
                vn.offset |= uniqueoffset;
            }
            else
                vn.offset = vn.space.wrapOffset(vntpl.getOffset().fix(*walker));
        }

        /// \brief Generate a pointer VarnodeData from a dynamic template (VarnodeTpl)
        ///
        /// The symbol represents a value referenced through a dynamic pointer.
        /// This method generates the varnode representing the pointer itself and also
        /// returns the address space in anticipation of generating the LOAD or STORE
        /// that actually manipulates the value.
        /// \param vntpl is the dynamic template to reference
        /// \param vn is the object to fill with concrete values
        /// \return the address space being pointed to
        private AddrSpace generatePointer(VarnodeTpl vntpl, VarnodeData vn)
        {
            FixedHandle hand = walker.getFixedHandle(vntpl.getOffset().getHandleIndex());
            vn.space = hand.offset_space;
            vn.size = hand.offset_size;
            if (vn.space == const_space)
                vn.offset = hand.offset_offset & Globals.calc_mask(vn.size);
            else if (vn.space == uniq_space)
                vn.offset = hand.offset_offset | uniqueoffset;
            else
                vn.offset = vn.space.wrapOffset(hand.offset_offset);
            return hand.space;
        }

        private void generatePointerAdd(PcodeData op, VarnodeTpl vntpl)
        {
            ulong offsetPlus = vntpl.getOffset().getReal() & 0xffff;
            if (offsetPlus == 0)
            {
                return;
            }
            PcodeData* nextop = cache.allocateInstruction();
            nextop.opc = op.opc;
            nextop.invar = op.invar;
            nextop.isize = op.isize;
            nextop.outvar = op.outvar;
            op.isize = 2;
            op.opc = OpCode.CPUI_INT_ADD;
            VarnodeData* newparams = op.invar = cache.allocateVarnodes(2);
            newparams[0] = nextop.invar[1];
            newparams[1].space = const_space;   // Add in V_OFFSET_PLUS
            newparams[1].offset = offsetPlus;
            newparams[1].size = newparams[0].size;
            op.outvar = nextop.invar + 1; // Output of ADD is input to original op
            op.outvar.space = uniq_space;     // Result of INT_ADD in special runtime temp
            op.outvar.offset = uniq_space.getTrans().getUniqueStart(Translate::RUNTIME_BITRANGE_EA);
        }

        /// Set uniquifying bits for the current instruction
        /// Bits used to make temporary registers unique across multiple instructions
        /// are generated based on the given address.
        /// \param addr is the given Address
        private void setUniqueOffset(Address addr)
        {
            uniqueoffset = (addr.getOffset() & uniquemask) << 4;
        }

        /// \brief Constructor
        ///
        /// \param w is the parsed instruction
        /// \param dcache is a cache of nearby instruction parses
        /// \param pc will hold the PcodeData and VarnodeData objects produced by \b this builder
        /// \param cspc is the constant address space
        /// \param uspc is the unique address space
        /// \param umask is the mask to use to find unique bits within an Address
        public SleighBuilder(ParserWalker w, DisassemblyCache dcache, PcodeCacher pc, AddrSpace cspc, AddrSpace uspc, uint umask)
            : base(0)
        {
            walker = w;
            discache = dcache;
            cache = pc;
            const_space = cspc;
            uniq_space = uspc;
            uniquemask = umask;
            uniqueoffset = (walker.getAddr().getOffset() & uniquemask) << 4;
        }

        private override void appendBuild(OpTpl bld, int secnum)
        {
            // Append p-code for a particular build statement
            int index = bld.getIn(0).getOffset().getReal(); // Recover operand index from build statement
                                                               // Check if operand is a subtable
            SubtableSymbol* sym = (SubtableSymbol*)walker.getConstructor().getOperand(index).getDefiningSymbol();
            if ((sym == (SubtableSymbol)null) || (sym.getType() !=  SleighSymbol.symbol_type.subtable_symbol)) return;

            walker.pushOperand(index);
            Constructor* ct = walker.getConstructor();
            if (secnum >= 0)
            {
                ConstructTpl* construct = ct.getNamedTempl(secnum);
                if (construct == (ConstructTpl)null)
                    buildEmpty(ct, secnum);
                else
                    build(construct, secnum);
            }
            else
            {
                ConstructTpl* construct = ct.getTempl();
                build(construct, -1);
            }
            walker.popOperand();
        }

        private override void delaySlot(OpTpl op)
        {
            // Append pcode for an entire instruction (delay slot)
            // in the middle of the current instruction
            ParserWalker* tmp = walker;
            ulong olduniqueoffset = uniqueoffset;

            Address baseaddr = tmp.getAddr();
            int fallOffset = tmp.getLength();
            int delaySlotByteCnt = tmp.getParserContext().getDelaySlot();
            int bytecount = 0;
            do
            {
                Address newaddr = baseaddr + fallOffset;
                setUniqueOffset(newaddr);
                ParserContext pos = discache.getParserContext(newaddr);
                if (pos.getParserState() != ParserContext::pcode)
                    throw new LowlevelError("Could not obtain cached delay slot instruction");
                int len = pos.getLength();

                ParserWalker newwalker(pos );
                walker = &newwalker;
                walker.baseState();
                build(walker.getConstructor().getTempl(), -1); // Build the whole delay slot
                fallOffset += len;
                bytecount += len;
            } while (bytecount < delaySlotByteCnt);
            walker = tmp;           // Restore original context
            uniqueoffset = olduniqueoffset;
        }

        private override void setLabel(OpTpl op)
        {
            cache.addLabel(op.getIn(0).getOffset().getReal() + getLabelBase());
        }

        private override void appendCrossBuild(OpTpl bld, int secnum)
        {
            // Weave in the p-code section from an instruction at another address
            // bld-param(0) contains the address of the instruction
            // bld-param(1) contains the section number
            if (secnum >= 0)
                throw new LowlevelError("CROSSBUILD directive within a named section");
            secnum = bld.getIn(1).getOffset().getReal();
            VarnodeTpl* vn = bld.getIn(0);
            AddrSpace* spc = vn.getSpace().fixSpace(*walker);
            ulong addr = spc.wrapOffset(vn.getOffset().fix(*walker));

            ParserWalker* tmp = walker;
            ulong olduniqueoffset = uniqueoffset;

            Address newaddr(spc, addr);
            setUniqueOffset(newaddr);
            ParserContext pos = discache.getParserContext(newaddr);
            if (pos.getParserState() != ParserContext::pcode)
                throw new LowlevelError("Could not obtain cached crossbuild instruction");

            ParserWalker newwalker(pos, tmp.getParserContext() );
            walker = &newwalker;

            walker.baseState();
            Constructor* ct = walker.getConstructor();
            ConstructTpl* construct = ct.getNamedTempl(secnum);
            if (construct == (ConstructTpl)null)
                buildEmpty(ct, secnum);
            else
                build(construct, secnum);
            walker = tmp;
            uniqueoffset = olduniqueoffset;
        }
    }
}
