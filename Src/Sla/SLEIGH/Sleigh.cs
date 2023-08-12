using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.SLEIGH
{
    /** \page sleigh SLEIGH

      \section sleightoc Table of Contents

        - \ref sleighoverview
        - \ref sleighbuild
        - \ref sleighuse
        - \subpage sleighAPIbasic
        - \subpage sleighAPIemulate

      \b Key \b Classes
        - \ref Translate
        - \ref AssemblyEmit
        - \ref PcodeEmit
        - \ref LoadImage
        - \ref ContextDatabase

      \section sleighoverview Overview

      Welcome to \b SLEIGH, a machine language translation and
      dissassembly engine.  SLEIGH is both a processor
      specification language and the associated library and
      tools for using such a specification to generate assembly
      and to generate \b pcode, a reverse engineering Register
      Transfer Language (RTL), from binary machine instructions.
      
      SLEIGH was originally based on \b SLED, a
      \e Specification \e Language \e for \e Encoding \e and
      \e Decoding, designed by Norman Ramsey and Mary F. Fernandez,
      which performed disassembly (and assembly).  SLEIGH
      extends SLED by providing semantic descriptions (via the
      RTL) of machine instructions and other practical enhancements
      for doing real world reverse engineering. 

      SLEIGH is part of Project \b GHIDRA. It provides the core
      of the GHIDRA disassembler and the data-flow and
      decompilation analysis.  However, SLEIGH can serve as a
      standalone library for use in other applications for
      providing a generic disassembly and RTL translation interface.

      \section sleighbuild Building SLEIGH

      There are a couple of \e make targets for building the SLEIGH
      library from source.  These are:

      \code
         make libsla.a               # Build the main library

         make libsla_dbg.a           # Build the library with debug symbols
      \endcode

      The source code file \e sleighexample.cc has a complete example
      of initializing the Translate engine and using it to generate
      assembly and pcode.  The source has a hard-coded file name,
      \e x86testcode, as the example binary executable it attempts
      to decode, but this can easily be changed.  It also needs
      a SLEIGH specification file (\e .sla) to be present.

      Building the example application can be done with something
      similar to the following makefile fragment.

      \code
        # The C compiler
        CXX=g++

        # Debug flags
        DBG_CXXFLAGS=-g -Wall -Wno-sign-compare

        OPT_CXXFLAGS=-O2 -Wall -Wno-sign-compare

        # libraries
        INCLUDES=-I./src

        LNK=src/libsla_dbg.a

        sleighexample.o:      sleighexample.cc
              $(CXX) -c $(DBG_CXXFLAGS) -o sleighexample sleighexample.o $(LNK)
      
        clean:
              rm -rf *.o sleighexample
      \endcode

      \section sleighuse Using SLEIGH

      SLEIGH is a generic reverse engineering tool in the sense
      that the API is designed to be completely processor
      independent.  In order to process binary executables for a
      specific processor, The library reads in a \e
      specification \e file, which describes how instructions
      are encoded and how they are interpreted by the processor.
      An application which needs to do disassembly or generate
      \b pcode can design to the SLEIGH API once, and then the
      application will automatically support any processor for
      which there is a specification.
      
      For working with a single processor, the SLEIGH library
      needs to load a single \e compiled form of the processor
      specification, which is traditionally given a ".sla" suffix.
      Most common processors already have a ".sla" file available.
      So to use SLEIGH with these processors, the library merely
      needs to be made aware of the desired file.  This documentation
      covers the use of the SLEIGH API, assuming that this
      specification file is available.

      The ".sla" files themselves are created by running
      the \e compiler on a file written in the formal SLEIGH
      language.  These files traditionally have the suffix ".slaspec"
      For those who want to design such a specification for a new
      processor, please refer to the document, "SLEIGH: A Language
      for Rapid Processor Specification."

     */

    /**
     \page sleighAPIbasic The Basic SLEIGH Interface

     To use SLEIGH as a library within an application, there
     are basically five classes that you need to be aware of.

       - \ref sleightranslate
       - \ref sleighassememit
       - \ref sleighpcodeemit
       - \ref sleighloadimage
       - \ref sleighcontext
         
     \section sleightranslate Translate (or Sleigh)

     The core SLEIGH class is Sleigh, which is derived from the
     interface, Translate.  In order to instantiate it in your code,
     you need a LoadImage object, and a ContextDatabase object.
     The load image is responsible for retrieving instruction
     bytes, based on address, from a binary executable. The context
     database provides the library extra mode information that may
     be necessary to do the disassembly or translation.  This can
     be used, for instance, to specify that an x86 binary is running
     in 32-bit mode, or to specify that an ARM processor is running
     in THUMB mode.  Once these objects are built, the Sleigh
     object can be immediately instantiated.

     \code
     LoadImageBfd *loader;
     ContextDatabase *context;
     Translate *trans;

     // Set up the loadimage
     // Providing an executable name and architecture
     string loadimagename = "x86testcode";
     string bfdtarget= "default";

     loader = new LoadImageBfd(loadimagename,bfdtarget);
     loader.open();       // Load the executable from file

     context = new ContextInternal();   // Create a processor context

     trans = new Sleigh(loader,context);  // Instantiate the translator
     \endcode

     Once the Sleigh object is in hand, the only required
     initialization step left is to inform it of the ".sla" file.
     The file is in XML format and needs to be read in using
     SLEIGH's built-in XML parser. The following code accomplishes
     this.

     \code
     string sleighfilename = "specfiles/x86.sla";
     DocumentStorage docstorage;
     Element *sleighroot = docstorage.openDocument(sleighfilename).getRoot();
     docstorage.registerTag(sleighroot);
     trans.initialize(docstorage);  // Initialize the translator
     \endcode

     \section sleighassememit AssemblyEmit

     In order to do disassembly, you need to derive a class from
     AssemblyEmit, and implement the method \e dump.  The library
     will call this method exactly once, for each instruction
     disassembled.

     This routine simply needs to decide how (and where) to print
     the corresponding portion of the disassembly.  For instance,

     \code
     class AssemblyRaw : public AssemblyEmit {
     public:
       virtual void dump(Address addr, string mnem, string body) {
         addr.printRaw(cout);
         cout << ": " << mnem << ' ' << body << endl;
       }
     };
     \endcode

     This is a minimal implementation that simply dumps the
     disassembly straight to standard out.  Once this object is
     instantiated, the Sleigh object can use it to write out
     assembly via the Translate::printAssembly() method.

     \code
     AssemblyEmit *assememit = new AssemblyRaw();

     Address addr(trans.getDefaultCodeSpace(),0x80484c0);
     int length;                  // Length of instruction in bytes

     length = trans.printAssembly(*assememit,addr);
     addr = addr + length;        // Advance to next instruction
     length = trans.printAssembly(*assememit,addr);
     addr = addr + length;
     length = trans.printAssembly(*assememit,addr);
     \endcode

     \section sleighpcodeemit PcodeEmit

     In order to generate a \b pcode translation of a machine
     instruction, you need to derive a class from PcodeEmit and
     implement the virtual method \e dump. This method will be
     invoked once for each \b pcode operation in the translation
     of a machine instruction.  There will likely be multiple calls
     per instruction.  Each call passes in a single \b pcode
     operation, complete with its possible varnode output, and
     all of its varnode inputs.  Here is an example of a PcodeEmit
     object that simply prints out the \b pcode.

     \code
     class PcodeRawOut : public PcodeEmit {
     public:
       virtual void dump(Address addr,OpCode opc,VarnodeData *outvar,VarnodeData *vars,int isize);
     };

     static void print_vardata(ostream &s,VarnodeData &data)

     {
       s << '(' << data.space.getName() << ',';
       data.space.printOffset(s,data.offset);
       s << ',' << dec << data.size << ')';
     }

     void PcodeRawOut::dump(Address addr,OpCode opc,VarnodeData *outvar,VarnodeData *vars,int isize)

     {
       if (outvar != (VarnodeData *)0) {     // The output is optional
         print_vardata(cout,*outvar);
         cout << " = ";
       }
       cout << Globals.get_opname(opc);
       // Possibly check for a code reference or a space reference
       for(int i=0;i<isize;++i) {
         cout << ' ';
         print_vardata(cout,vars[i]);
       }
       cout << endl;
     }
     \endcode

     Notice that the \e dump routine uses the built-in function
     \e Globals.get_opname to find a string version of the opcode.  Each
     varnode is defined in terms of the VarnodeData object, which
     is defined simply:

     \code
     struct VarnodeData {
       AddrSpace *space;          // The address space
       ulong offset;              // The offset within the space
       uint size;                // The number of bytes at that location
     };
     \endcode

     Once the PcodeEmit object is instantiated, the Sleigh object can
     use it to generate pcode, one instruction at a time, using the
     Translate::oneInstruction() const method.

     \code
     PcodeEmit *pcodeemit = new PcodeRawOut();

     Address addr(trans.getDefaultCodeSpace(),0x80484c0);
     int length;                   // Length of instruction in bytes

     length = trans.oneInstruction(*pcodeemit,addr);
     addr = addr + length;         // Advance to next instruction
     length = trans.oneInstruction(*pcodeemit,addr);
     addr = addr + length;
     length = trans.oneInstruction(*pcodeemit,addr);
     \endcode

     For an application to properly \e follow \e flow, while translating
     machine instructions into pcode, the emitted pcode must be
     inspected for the various branch operations.

     \section sleighloadimage LoadImage

     A LoadImage holds all the binary data from an executable file
     in the format similar to how it would exist when being executed
     by a real processor.  The interface to this from SLEIGH is
     actually very simple, although it can hide a complicated
     structure.  One method does most of the work, LoadImage::loadFill().
     It takes a byte pointer, a size, and an Address. The method
     is expected to fill in the \e ptr array with \e size bytes
     taken from the load image, corresponding to the address \e addr.
     There are two more virtual methods that are required for a
     complete implementation of LoadImage, \e getArchType and
     \e adjustVma, but these do not need to be implemented fully.

     \code
     class MyLoadImage : public LoadImage {
     public:
       MyLoadImage(string &nm) : Loadimage(nm) {}
       virtual void loadFill(byte *ptr,int size,Address &addr);
       virtual string getArchType(void) { return "mytype"; }
       virtual void adjustVma(long adjust) {}
     };
     \endcode

     \section sleighcontext ContextDatabase

     The ContextDatabase needs to keep track of any possible
     context variable and its value, over different address ranges.
     In most cases, you probably don't need to override the class
     yourself, but can use the built-in class, ContextInternal.
     This provides the basic functionality required and will work
     for different architectures.  What you may need to do is
     set values for certain variables, depending on the processor
     and the environment it is running @in.  For instance, for
     the x86 platform, you need to set the \e addrsize and \e opsize
     bits, to indicate the processor would be running in 32-bit
     mode.  The context variables specific to a particular processor
     are established by the SLEIGH spec.  So the variables can
     only be set \e after the spec has been loaded.

     \code
       ...
       context = new ContextInternal();
       trans = new Sleigh(loader,context);
       DocumentStorage docstorage;
       Element *root = docstorage.openDocument("specfiles/x86.sla").getRoot();
       docstorage.registerTag(root);
       trans.initialize(docstorage);

       context.setVariableDefault("addrsize",1);  // Address size is 32-bits
       context.setVariableDefault("opsize",1);    // Operand size is 32-bits
     \endcode

     
    */
    /// \brief A full SLEIGH engine
    ///
    /// Its provided with a LoadImage of the bytes to be disassembled and
    /// a ContextDatabase.
    ///
    /// Assembly is produced via the printAssembly() method, provided with an
    /// AssemblyEmit object and an Address.
    ///
    /// P-code is produced via the oneInstruction() method, provided with a PcodeEmit
    /// object and an Address.
    internal class Sleigh : SleighBase
    {
        private LoadImage loader;          ///< The mapped bytes in the program
        private ContextDatabase context_db;        ///< Database of context values steering disassembly
        private ContextCache cache;            ///< Cache of recently used context values
        private /*mutable*/ DisassemblyCache? discache; ///< Cache of recently parsed instructions
        private /*mutable*/ PcodeCacher pcode_cache;    ///< Cache of p-code data just prior to emitting

        ///< Delete the context and disassembly caches
        private void clearForDelete()
        {
            // delete cache;
            //if (discache != (DisassemblyCache)null)
            //    delete discache;
        }

        /// \brief Obtain a parse tree for the instruction at the given address
        ///
        /// The tree may be cached from a previous access.  If the address
        /// has not been parsed, disassembly is performed, and a new parse tree
        /// is prepared.  Depending on the desired \e state, the parse tree
        /// can be prepared either for disassembly or for p-code generation.
        /// \param addr is the given address of the instruction
        /// \param state is the desired parse state.
        /// \return the parse tree object (ParseContext)
        protected ParserContext obtainContext(Address addr,ParserContext.State state)
        {
            ParserContext pos = discache.getParserContext(addr);
            ParserContext.State curstate = pos.getParserState();
            if (curstate >= state)
                return pos;
            if (curstate == ParserContext.State.uninitialized) {
                resolve(pos);
                if (state == ParserContext.State.disassembly)
                    return pos;
            }
            // If we reach here,  state must be ParserContext.State.pcode
            resolveHandles(pos);
            return pos;
        }

        ///< Generate a parse tree suitable for disassembly
        /// Resolve \e all the constructors involved in the instruction at the indicated address
        /// \param pos is the parse object that will hold the resulting tree
        private void resolve(ParserContext pos)
        {
            loader.loadFill(pos.getBuffer(), 16, pos.getAddr());
            ParserWalkerChange walker = new ParserWalkerChange(pos);
            pos.deallocateState(walker);    // Clear the previous resolve and initialize the walker
            Constructor ct;
            Constructor subct;
            uint off;
            int oper, numoper;

            pos.setDelaySlot(0);
            walker.setOffset(0);        // Initial offset
            pos.clearCommits();     // Clear any old context commits
            pos.loadContext();      // Get context for current address
            ct = root.resolve(walker); // Base constructor
            walker.setConstructor(ct);
            ct.applyContext(walker);
            while (walker.isState()) {
                ct = walker.getConstructor();
                oper = walker.getOperand();
                numoper = ct.getNumOperands();
                while (oper < numoper) {
                    OperandSymbol sym = ct.getOperand(oper);
                    off = walker.getOffset(sym.getOffsetBase()) + sym.getRelativeOffset();
                    pos.allocateOperand(oper, walker); // Descend into new operand and reserve space
                    walker.setOffset(off);
                    TripleSymbol tsym = sym.getDefiningSymbol();
                    if (tsym != (TripleSymbol)null) {
                        subct = tsym.resolve(walker);
                        if (subct != (Constructor)null) {
                            walker.setConstructor(subct);
                            subct.applyContext(walker);
                            break;
                        }
                    }
                    walker.setCurrentLength(sym.getMinimumLength());
                    walker.popOperand();
                    oper += 1;
                }
                if (oper >= numoper) {
                    // Finished processing constructor
                    walker.calcCurrentLength(ct.getMinimumLength(), numoper);
                    walker.popOperand();
                    // Check for use of delayslot
                    ConstructTpl templ = ct.getTempl();
                    if ((templ != (ConstructTpl)null) && (templ.delaySlot() > 0))
                        pos.setDelaySlot((int)templ.delaySlot());
                }
            }
            pos.setNaddr(pos.getAddr() + pos.getLength());  // Update Naddr to pointer after instruction
            pos.setParserState(ParserContext.State.disassembly);
        }

        ///< Prepare the parse tree for p-code generation
        /// Resolve handle templates for the given parse tree, assuming Constructors
        /// are already resolved.
        /// \param pos is the given parse tree
        private void resolveHandles(ParserContext pos)
        {
            TripleSymbol? triple;
            Constructor ct;
            int oper, numoper;

            ParserWalker walker = new ParserWalker(pos);
            walker.baseState();
            while (walker.isState()) {
                ct = walker.getConstructor();
                oper = walker.getOperand();
                numoper = ct.getNumOperands();
                while (oper < numoper) {
                    OperandSymbol sym = ct.getOperand(oper);
                    walker.pushOperand(oper);   // Descend into node
                    triple = sym.getDefiningSymbol();
                    if (triple != (TripleSymbol)null) {
                        if (triple.getType() ==  SleighSymbol.symbol_type.subtable_symbol)
                            break;
                        else            // Some other kind of symbol as an operand
                            triple.getFixedHandle(walker.getParentHandle(), walker);
                    }
                    else
                    {           // Must be an expression
                        PatternExpression patexp = sym.getDefiningExpression();
                        long res = patexp.getValue(walker);
                        FixedHandle hand = walker.getParentHandle();
                        hand.space = pos.getConstSpace(); // Result of expression is a constant
                        hand.offset_space = (AddrSpace)null;
                        hand.offset_offset = (ulong)res;
                        hand.size = 0;      // This size should not get used
                    }
                    walker.popOperand();
                    oper += 1;
                }
                if (oper >= numoper) {
                    // Finished processing constructor
                    ConstructTpl? templ = ct.getTempl();
                    if (templ != (ConstructTpl)null) {
                        HandleTpl res = templ.getResult();
                        if (res != (HandleTpl)null)   // Pop up handle to containing operand
                            res.fix(walker.getParentHandle(), walker);
                        // If we need an indicator that the constructor exports nothing try
                        // else
                        //   walker.getParentHandle().setInvalid();
                    }
                    walker.popOperand();
                }
            }
            pos.setParserState(ParserContext.State.pcode);
        }

        /// \param ld is the LoadImage to draw program bytes from
        /// \param c_db is the context database
        public Sleigh(LoadImage ld, ContextDatabase c_db)
            : base()
        {
            loader = ld;
            context_db = c_db;
            cache = new ContextCache(c_db);
            discache = (DisassemblyCache)null;
        }

        ~Sleigh()
        {
            clearForDelete();
        }

        ///< Reset the engine for a new program
        /// Completely clear everything except the base and reconstruct
        /// with a new LoadImage and ContextDatabase
        /// \param ld is the new LoadImage
        /// \param c_db is the new ContextDatabase
        public void reset(LoadImage ld, ContextDatabase c_db)
        {
            clearForDelete();
            pcode_cache.clear();
            loader = ld;
            context_db = c_db;
            cache = new ContextCache(c_db);
            discache = (DisassemblyCache)null;
        }

        /// The .sla file from the document store is loaded and cache objects are prepared
        /// \param store is the document store containing the main \<sleigh> tag.
        public override void initialize(DocumentStorage store)
        {
            if (!isInitialized())
            {   // Initialize the base if not already
                Element? el = store.getTag("sleigh");
                if (el == (Element)null)
                    throw new LowlevelError("Could not find sleigh tag");
                restoreXml(el);
            }
            else
                reregisterContext();
            uint parser_cachesize = 2;
            uint parser_windowsize = 32;
            if ((maxdelayslotbytes > 1) || (unique_allocatemask != 0)) {
                parser_cachesize = 8;
                parser_windowsize = 256;
            }
            discache = new DisassemblyCache(this, cache, getConstantSpace(), (int)parser_cachesize,
                (int)parser_windowsize);
        }

        public override void registerContext(string name,int sbit, int ebit)
        {
            context_db.registerVariable(name, sbit, ebit);
        }

        public override void setContextDefault(string name, uint val)
        {
            context_db.setVariableDefault(name, val);
        }

        public override void allowContextSet(bool val)
        {
            cache.allowSet(val);
        }

        public override int instructionLength(Address baseaddr)
        {
            ParserContext pos = obtainContext(baseaddr, ParserContext.State.disassembly);
            return pos.getLength();
        }

        public override int oneInstruction(PcodeEmit emit, Address baseaddr)
        {
            int fallOffset;
            if (alignment != 1) {
                if ((baseaddr.getOffset() % (uint)alignment) != 0) {
                    throw new UnimplError($"Instruction address not aligned: {baseaddr}", 0);
                }
            }

            ParserContext pos = obtainContext(baseaddr, ParserContext.State.pcode);
            pos.applyCommits();
            fallOffset = pos.getLength();

            if (pos.getDelaySlot() > 0) {
                int bytecount = 0;
                do {
                    // Do not pass pos.getNaddr() to obtainContext, as pos may have been previously cached and had naddr adjusted
                    ParserContext delaypos = obtainContext(pos.getAddr() + fallOffset,
                        ParserContext.State.pcode);
                    delaypos.applyCommits();
                    int len = delaypos.getLength();
                    fallOffset += len;
                    bytecount += len;
                } while (bytecount < pos.getDelaySlot());
                pos.setNaddr(pos.getAddr() + fallOffset);
            }
            ParserWalker walker = new ParserWalker(pos);
            walker.baseState();
            pcode_cache.clear();
            SleighBuilder builder = new SleighBuilder(walker, discache, pcode_cache, getConstantSpace(), getUniqueSpace(), unique_allocatemask);
            try {
                builder.build(walker.getConstructor().getTempl(), -1);
                pcode_cache.resolveRelatives();
                pcode_cache.emit(baseaddr, emit);
            }
            catch (UnimplError) {
                StringWriter s = new StringWriter();
                s.Write("Instruction not implemented in pcode:\n ");
                ParserWalker cur = builder.getCurrentWalker();
                cur.baseState();
                Constructor ct = cur.getConstructor();
                cur.getAddr().printRaw(s);
                s.Write(": ");
                ct.printMnemonic(s, cur);
                s.Write("  ");
                ct.printBody(s, cur);
                throw new UnimplError(s.ToString(), fallOffset);
            }
            return fallOffset;
        }

        public override int printAssembly(AssemblyEmit emit, Address baseaddr)
        {
            int sz;

            ParserContext pos = obtainContext(baseaddr, ParserContext.State.disassembly);
            ParserWalker walker = new ParserWalker(pos);
            walker.baseState();

            Constructor ct = walker.getConstructor();
            TextWriter mons = new StringWriter();
            ct.printMnemonic(mons, walker);
            TextWriter body = new StringWriter();
            ct.printBody(body, walker);
            emit.dump(baseaddr, mons.ToString(), body.ToString());
            sz = pos.getLength();
            return sz;
        }
    }
}
