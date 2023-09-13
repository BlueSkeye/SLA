using Sla.CORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;
using System.Xml.Linq;
using System.Collections;
using System.Runtime.Intrinsics;
using System.Drawing;

namespace Sla.DECCORE
{
    /// \brief Manager for all the major decompiler subsystems
    /// An instantiation is tailored to a specific LoadImage,
    /// processor, and compiler spec. This class is the \e owner of
    /// the LoadImage, Translate, symbols (Database), PrintLanguage, etc.
    /// This class also holds numerous configuration parameters for the analysis process
    internal abstract class Architecture : AddrSpaceManager
    {
        /// ID string uniquely describing this architecture
        public string archid;

        // Configuration data
        // How many levels to let parameter trims recurse
        public int trim_recurse_max;
        // Maximum number of references to an implied var
        public int max_implied_ref;
        // Max terms duplicated without a new variable
        public int max_term_duplication;
        // Maximum size of an "integer" type before creating an array type
        public int max_basetype_size;
        // Minimum size of a function symbol
        public int min_funcsymbol_size;
        // Maximum number of entries in a single JumpTable
        public uint max_jumptable_size;
        // Aggressively trim inputs that look like they are sign extended
        public bool aggressive_ext_trim;
        // true if readonly values should be treated as constants
        public bool readonlypropagate;
        // True if we should infer pointers from constants that are likely addresses
        public bool infer_pointers;
        // True if we should attempt conversion of \e whiledo loops to \e for loops
        public bool analyze_for_loops;
        // Set of address spaces in which a pointer constant is inferable
        public List<AddrSpace> inferPtrSpaces = new List<AddrSpace>();
        // How many bits of alignment a function ptr has
        public int funcptr_align;
        // options passed to flow following engine
        public FlowInfo.FlowFlag flowoptions;
        // Maximum instructions that can be processed in one function
        public uint max_instructions;
        // Aliases blocked by 0=none, 1=struct, 2=array, 3=all
        public int alias_block_level;
        // Toggle for data-types splitting: Bit 0=structs, 1=arrays, 2=pointers
        public OptionSplitDatatypes.Options split_datatype_config;
        // Extra rules that go in the main pool (cpu specific, experimental)
        public List<Rule> extra_pool_rules = new List<Rule>();

        // Memory map of global variables and functions
        public Database? symboltab;
        // Map from addresses to context settings
        public ContextDatabase? context;
        // Parsed forms of possible prototypes
        public Dictionary<string, ProtoModel> protoModels =
            new Dictionary<string, ProtoModel>();
        // Parsed form of default prototype
        public ProtoModel? defaultfp;
        // Default storage location of return address (for current function)
        public VarnodeData defaultReturnAddr;
        // Function proto to use when evaluating current function
        public ProtoModel? evalfp_current;
        // Function proto to use when evaluating called functions
        public ProtoModel? evalfp_called;
        // List of types for this binary
        public TypeFactory? types;
        // Translation method for this binary
        public Translate? translate;
        // Method for loading portions of binary
        public LoadImage? loader;
        // Pcode injection manager
        public PcodeInjectLibrary? pcodeinjectlib;
        // Ranges for which high-level pointers are not possible
        public RangeList nohighptr;
        // Comments for this architecture
        public CommentDatabase? commentdb;
        // Manager of decoded strings
        public StringManager? stringManager;
        // Deferred constant values
        public ConstantPool? cpool;
        // Current high-level language printer
        public PrintLanguage print;
        // List of high-level language printers supported
        public List<PrintLanguage> printlist = new List<PrintLanguage>();
        // Options that can be configured
        public OptionDatabase options;
        // Registered p-code instructions
        public List<TypeOp> inst = new List<TypeOp>();
        // Specifically registered user-defined p-code ops
        public UserOpManage userops;
        // registers that we would prefer to see split for this processor
        public List<PreferSplitRecord> splitrecords = new List<PreferSplitRecord>();
        // Vector registers that have preferred lane sizes
        public List<LanedRegister> lanerecords = new List<LanedRegister>();
        // Actions that can be applied in this architecture
        public ActionDatabase allacts;
        // True if loader symbols have been read
        public bool loadersymbols_parsed;
#if CPUI_STATISTICS
        /// Statistics collector
        public Statistics stats;
#endif
#if OPACTION_DEBUG
        /// The error console
        public TextWriter debugstream;
#endif
        /// Construct an uninitialized Architecture
        /// Set most sub-components to null pointers. Provide reasonable defaults
        /// for the configurable options
        public Architecture()
        {
            //  endian = -1;
            resetDefaultsInternal();
            min_funcsymbol_size = 1;
            aggressive_ext_trim = false;
            funcptr_align = 0;
            defaultfp = null;
            defaultReturnAddr.space = null;
            evalfp_current = null;
            evalfp_called = null;
            types = null;
            translate = null;
            loader = null;
            pcodeinjectlib = null;
            commentdb = null;
            stringManager = null;
            cpool = null;
            symboltab = null;
            context = null;
            print = PrintLanguageCapability.getDefault().buildLanguage(this);
            printlist.Add(print);
            options = new OptionDatabase(this);
            loadersymbols_parsed = false;
#if CPUI_STATISTICS
            stats = new Statistics();
#endif
#if OPACTION_DEBUG
            debugstream = (ostream*)0;
#endif
        }

        /// Release resources for all sub-components
        ~Architecture()
        {
            // Delete anything that was allocated
            foreach (TypeOp t_op in inst) {
                if (null != t_op) {
                    // delete t_op;
                }
            }
            for (int i = 0; i < extra_pool_rules.Count; ++i) {
                // delete extra_pool_rules[i];
            }

            if (null != symboltab) {
                // delete symboltab;
            }
            for (int i=0;i<printlist.Count;++i) {
                // delete printlist[i];
            }
            // delete options;
#if CPUI_STATISTICS
            // delete stats;
#endif
            foreach (ProtoModel piter in protoModels.Values) {
                // delete piter;
            }

            if (null != types) {
                // delete types;
            }
            if (null != translate) {
                // delete translate;
            }
            if (null != loader) {
                // delete loader;
            }
            if (null != pcodeinjectlib) {
                // delete pcodeinjectlib;
            }
            if (null != commentdb) {
                // delete commentdb;
            }
            if (null != stringManager) {
                // delete stringManager;
            }
            if (null != cpool) {
                // delete cpool;
            }
            if (null != context) {
                // delete context;
            }
        }

        /// Load the image and configure architecture
        /// Create the LoadImage and load the executable to be analyzed.
        /// Using this and possibly other initialization information, create
        /// all the sub-components necessary for a complete Architecture
        /// The DocumentStore may hold previously gleaned configuration information
        /// and is used to read in other configuration files while initializing.
        /// \param store is the XML document store
        public void init(DocumentStorage store)
        {
            // Loader is built first
            buildLoader(store);
            resolveArchitecture();
            buildSpecFile(store);

            buildContext(store);
            buildTypegrp(store);
            buildCommentDB(store);
            buildStringManager(store);
            buildConstantPool(store);
            buildDatabase(store);

            restoreFromSpec(store);
            print.initializeFromArchitecture();
            // In case the specs created additional address spaces
            symboltab.adjustCaches();
            buildSymbols(store);
            // Let subclasses do things after translate is ready
            postSpecFile();
            // Must be called after translate is built
            buildInstructions(store);
            fillinReadOnlyFromLoader();
        }

        /// Reset default values for options specific to Architecture
        public void resetDefaultsInternal()
        {
            trim_recurse_max = 5;
            // 2 is best, in specific cases a higher number might be good
            max_implied_ref = 2;
            // 2 and 3 (4) are reasonable
            max_term_duplication = 2;
            // Needs to be 8 or bigger
            max_basetype_size = 10;
            flowoptions = FlowInfo.FlowFlag.error_toomanyinstructions;
            max_instructions = 100000;
            infer_pointers = true;
            analyze_for_loops = true;
            readonlypropagate = false;
            // Block structs and arrays by default, but not more primitive data-types
            alias_block_level = 2;
            split_datatype_config = OptionSplitDatatypes.Options.option_struct |
                OptionSplitDatatypes.Options.option_array |
                OptionSplitDatatypes.Options.option_pointer;
            max_jumptable_size = 1024;
        }

        /// Reset defaults values for options owned by \b this
        /// Reset options that can be modified by the OptionDatabase. This includes
        /// options specific to this class and options under PrintLanguage and ActionDatabase
        public void resetDefaults()
        {
            resetDefaultsInternal();
            allacts.resetDefaults();
            for (int i = 0; i < printlist.Count; ++i) {
                printlist[i].resetDefaults();
            }
        }

        /// Get a specific PrototypeModel
        /// The Architecture maintains the set of prototype models that can
        /// be applied for this particular executable. Retrieve one by name.
        /// If the model doesn't exist, null is returned.
        /// \param nm is the name
        /// \return the matching model or null
        public ProtoModel? getModel(string nm)
        {
            ProtoModel? result;
            return protoModels.TryGetValue(nm, out result) ? result : null;
        }

        /// Does this Architecture have a specific PrototypeModel
        /// \param nm is the name of the model
        /// \return \b true if this Architecture supports a model with that name
        public bool hasModel(string nm) => protoModels.ContainsKey(nm);

        /// Create a model for an unrecognized name
        /// A new UnknownProtoModel, which clones its behavior from the default model, is created and associated with the
        /// unrecognized name.  Subsequent queries of the name return this new model.
        /// \param modelName is the unrecognized name
        /// \return the new \e unknown prototype model associated with the name
        public ProtoModel createUnknownModel(string modelName)
        {
            UnknownProtoModel model = new UnknownProtoModel(modelName, defaultfp);
            protoModels[modelName] = model;
            if (modelName == "unknown") {
                // "unknown" is a reserved/internal name
                // don't print it in declarations
                model.setPrintInDecl(false);
            }
            return model;
        }

        /// Are pointers possible to the given location?
        /// The Translate object keeps track of address ranges for which
        /// it is effectively impossible to have a pointer into. This is
        /// used for pointer aliasing calculations.  This routine returns
        /// \b true if it is \e possible to have pointers into the indicated
        /// range.
        /// \param loc is the starting address of the range
        /// \param size is the size of the range in bytes
        /// \return \b true if pointers are possible
        public bool highPtrPossible(Address loc, int size)
        {
            return (loc.getSpace().getType() != spacetype.IPTR_INTERNAL)
                && !nohighptr.inRange(loc, size);
        }

        /// Get space associated with a \e spacebase register
        /// Get the address space associated with the indicated
        /// \e spacebase register. I.e. if the location of the
        /// \e stack \e pointer is passed in, this routine would return
        /// a pointer to the \b stack space. An exception is thrown
        /// if no corresponding space is found.
        /// \param loc is the location of the \e spacebase register
        /// \param size is the size of the register in bytes
        /// \return a pointer to the address space
        public AddrSpace getSpaceBySpacebase(Address loc, int size)
        {
            int sz = numSpaces();
            for (int i = 0; i < sz; ++i) {
                AddrSpace? id = getSpace(i);
                if (null == id) {
                    continue;
                }
                int numspace = id.numSpacebase();
                for (int j = 0; j < numspace; ++j) {
                    VarnodeData point = id.getSpacebase(j);
                    if (   (point.size == size)
                        && (point.space == loc.getSpace())
                        && (point.offset == loc.getOffset()))
                    {
                        return id;
                    }
                }
            }
            throw new CORE.LowlevelError("Unable to find entry for spacebase register");
        }

        /// Get LanedRegister associated with storage
        /// Look-up the laned register record associated with a specific storage location. Currently, the
        /// record is only associated with the \e size of the storage, not its address. If there is no
        /// associated record, null is returned.
        /// \param loc is the starting address of the storage location
        /// \param size is the size of the storage in bytes
        /// \return the matching LanedRegister record or null
        public LanedRegister? getLanedRegister(Address loc, int size)
        {
            int min = 0;
            int max = lanerecords.Count - 1;
            while (min <= max) {
                int mid = (min + max) / 2;
                int sz = lanerecords[mid].getWholeSize();
                if (sz < size) {
                    min = mid + 1;
                }
                else if (size < sz) {
                    max = mid - 1;
                }
                else {
                    return lanerecords[mid];
                }
            }
            return null;
        }

        /// Get the minimum size of a laned register in bytes
        /// Return a size intended for comparison with a Varnode size to immediately determine if
        /// the Varnode is a potential laned register. If there are no laned registers for the architecture,
        /// -1 is returned.
        /// \return the size in bytes of the smallest laned register or -1.
        public int getMinimumLanedRegisterSize()
        {
            return (0 == lanerecords.Count) ? -1 : lanerecords[0].getWholeSize();
        }

        /// Set the default PrototypeModel
        /// The default model is used whenever an explicit model is not known
        /// or can't be determined.
        /// \param model is the ProtoModel object to make the default
        public void setDefaultModel(ProtoModel model)
        {
            if (null != defaultfp) {
                defaultfp.setPrintInDecl(true);
            }
            model.setPrintInDecl(false);
            defaultfp = model;
        }

        /// Clear analysis specific to a function
        /// Throw out the syntax tree, (unlocked) symbols, comments, and other derived information
        /// about a single function.
        /// \param fd is the function to clear
        public void clearAnalysis(Funcdata fd)
        {
            // Clear stuff internal to function
            fd.clear();
            // Clear out any analysis generated comments
            commentdb.clearType(fd.getAddress(),
                Comment.comment_type.warning | Comment.comment_type.warningheader);
        }

        /// Read any symbols from loader into database
        /// Symbols do not necessarily need to be available for the decompiler.
        /// This routine loads all the \e load \e image knows about into the symbol table
        /// \param delim is the delimiter separating namespaces from symbol base names
        public void readLoaderSymbols(string delim)
        {
            if (loadersymbols_parsed) {
                // already read
                return;
            }
            loader.openSymbols();
            loadersymbols_parsed = true;
            LoadImageFunc record = new LoadImageFunc();
            while (loader.getNextSymbol(record)) {
                string basename;
                Scope scope = symboltab.findCreateScopeFromSymbolName(record.name, delim,
                    out basename, null);
                scope.addFunction(record.address, basename);
            }
            loader.closeSymbols();
        }

        /// Provide a list of OpBehavior objects
        /// For all registered p-code opcodes, return the corresponding OpBehavior object.
        /// The object pointers are provided in a list indexed by OpCode.
        /// \param behave is the list to be populated
        public void collectBehaviors(List<OpBehavior> behave)
        {
            behave.Resize(inst.Count, null);
            for (int i = 0; i < inst.size(); ++i) {
                TypeOp? op = inst[i];
                if (null == op) {
                    continue;
                }
                behave[i] = op.getBehavior();
            }
        }

        /// Retrieve the \e segment op for the given space if any
        /// This method searches for a user-defined segment op registered
        /// for the given space.
        /// \param spc is the address space to check
        /// \return the SegmentOp object or null
        public SegmentOp? getSegmentOp(AddrSpace spc)
        {
            if (spc.getIndex() >= userops.numSegmentOps()) {
                return null;
            }
            SegmentOp? segdef = userops.getSegmentOp(spc.getIndex());
            if (null == segdef) {
                return null;
            }
            return (null == segdef.getResolve().space) ? null : segdef;
        }

        /// Set the prototype for a particular function
        /// Establish details of the prototype for a given function symbol
        /// \param pieces holds the raw prototype information and the symbol name
        public void setPrototype(PrototypePieces pieces)
        {
            string basename;
            Scope? scope = symboltab.resolveScopeFromSymbolName(pieces.name, "::",
                out basename, null);
            if (null == scope)
                throw new ParseError($"Unknown namespace: {pieces.name}");
            Funcdata? fd = scope.queryFunction(basename);
            if (null == fd) {
                throw new ParseError($"Unknown function name: {pieces.name}");
            }
            fd.getFuncProto().setPieces(pieces);
        }

        /// Establish a particular output language
        /// The decompiler supports one or more output languages (C, Java). This method
        /// does the main work of selecting one of the supported languages.
        /// In addition to selecting the main PrintLanguage object, this triggers
        /// configuration of the cast strategy and p-code op behaviors.
        /// \param nm is the name of the language
        public void setPrintLanguage(string nm)
        {
            for (int i = 0; i < printlist.Count; ++i) {
                if (printlist[i].getName() == nm) {
                    print = printlist[i];
                    print.adjustTypeOperators();
                    return;
                }
            }
            PrintLanguageCapability? capa = PrintLanguageCapability.findCapability(nm);
            if (null == capa)
                throw new CORE.LowlevelError($"Unknown print language: {nm}");
            // Copy settings for current print language
            bool printMarkup = print.emitsMarkup();
            TextWriter t = print.getOutputStream();
            print = capa.buildLanguage(this);
            // Restore settings from previous language
            print.setOutputStream(t);
            print.initializeFromArchitecture();
            if (printMarkup) {
                print.setMarkup(true);
            }
            printlist.Add(print);
            print.adjustTypeOperators();
            return;
        }

        /// Mark \e all spaces as global
        /// Set all spacetype.IPTR_PROCESSOR and spacetype.IPTR_SPACEBASE spaces to be global
        public void globalify()
        {
            Scope scope = symboltab.getGlobalScope() ?? throw new ApplicationException();
            int nm = numSpaces();

            for (int i = 0; i < nm; ++i) {
                AddrSpace? spc = getSpace(i);
                if (null == spc) {
                    continue;
                }
                if ((spc.getType() != spacetype.IPTR_PROCESSOR) && (spc.getType() != spacetype.IPTR_SPACEBASE)) {
                    continue;
                }
                symboltab.addRange(scope, spc, 0UL, spc.getHighest());
            }
        }

        /// Set flow overrides from XML
        /// Insert a series of out-of-band flow overrides based on a \<flowoverridelist> element.
        /// \param decoder is the stream decoder
        public void decodeFlowOverride(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_FLOWOVERRIDELIST);
            while(true) {
                uint subId = decoder.openElement();
                if (subId != ElementId.ELEM_FLOW) {
                    break;
                }
                string flowType = decoder.readString(AttributeId.ATTRIB_TYPE);
                Address funcaddr = Address.decode(decoder);
                Address overaddr = Address.decode(decoder);
                Funcdata? fd = symboltab.getGlobalScope().queryFunction(funcaddr);
                if (null != fd) {
                    fd.getOverride().insertFlowOverride(overaddr, Override.stringToType(flowType));
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        /// Get a string describing \b this architecture
        public virtual string getDescription() => archid;

        /// \brief Print an error message to console
        /// Write the given message to whatever the registered error stream is
        /// \param message is the error message
        public abstract void printMessage(string message);

        /// Encode \b this architecture to a stream
        /// Write the current state of all types, symbols, functions, etc. to a stream.
        /// \param encoder is the stream encoder
        public virtual void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_SAVE_STATE);
            encoder.writeBool(AttributeId.ATTRIB_LOADERSYMBOLS, loadersymbols_parsed);
            types.encode(encoder);
            symboltab.encode(encoder);
            context.encode(encoder);
            commentdb.encode(encoder);
            stringManager.encode(encoder);
            if (!cpool.empty()) {
                cpool.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_SAVE_STATE);
        }

        /// Restore the Architecture state from XML documents
        /// Read in all the sub-component state from a \<save_state> XML tag
        /// When adding stuff to this BEWARE: The spec file has already initialized stuff
        /// \param store is document store containing the parsed root tag
        public virtual void restoreXml(DocumentStorage store)
        {
            Element? el = store.getTag(ElementId.ELEM_SAVE_STATE.getName());
            if (null == el) {
                throw new CORE.LowlevelError("Could not find save_state tag");
            }
            XmlDecode decoder = new XmlDecode(this, el);
            uint elemId = decoder.openElement(ElementId.ELEM_SAVE_STATE);
            loadersymbols_parsed = false;
            while (true) {
                uint attribId = decoder.getNextAttributeId();
                if (0 == attribId) {
                    break;
                }
                if (attribId == AttributeId.ATTRIB_LOADERSYMBOLS) {
                    loadersymbols_parsed = decoder.readBool();
                }
            }
            while (true) {
                uint subId = decoder.peekElement();
                if (0 == subId) {
                    break;
                }
                if (subId == ElementId.ELEM_TYPEGRP) {
                    types.decode(decoder);
                }
                else if (subId == ElementId.ELEM_DB) {
                    symboltab.decode(decoder);
                }
                else if (subId == ElementId.ELEM_CONTEXT_POINTS) {
                    context.decode(decoder);
                }
                else if (subId == ElementId.ELEM_COMMENTDB) {
                    commentdb.decode(decoder);
                }
                else if (subId == ElementId.ELEM_STRINGMANAGE) {
                    stringManager.decode(decoder);
                }
                else if (subId == ElementId.ELEM_CONSTANTPOOL) {
                    cpool.decode(decoder, types);
                }
                else if (subId == ElementId.ELEM_OPTIONSLIST) {
                    options.decode(decoder);
                }
                else if (subId == ElementId.ELEM_FLOWOVERRIDELIST) {
                    decodeFlowOverride(decoder);
                }
                else if (subId == ElementId.ELEM_INJECTDEBUG) {
                    pcodeinjectlib.decodeDebug(decoder);
                }
                else {
                    throw new CORE.LowlevelError("XML error restoring architecture");
                }
            }
            decoder.closeElement(elemId);
        }

        /// Pick a default name for a function
        /// If no better name is available, this method can be used to generate
        /// a function name based on its address
        /// \param addr is the address of the function
        /// \param name will hold the constructed name
        public virtual void nameFunction(Address addr, out string name)
        {
            StringWriter defname = new StringWriter();
            defname.Write("func_");
            addr.printRaw(defname);
            name = defname.ToString();
        }

#if OPACTION_DEBUG
        /// Establish the debug console stream
        public void setDebugStream(TextWriter s)
        {
            debugstream = s;
        }
    
        /// Print message to the debug stream
        public void printDebug(string message)
        {
            debugstream.WriteLine(message);
        }
#endif
        /// \brief Create a new address space associated with a pointer register
        ///
        /// This process sets up a \e register \e relative"space for this architecture.
        /// If indicated, this space takes on the role of the \e formal stack space.
        /// Should only be called once during initialization.
        /// \param basespace is the address space underlying the stack
        /// \param nm is the name of the new space
        /// \param ptrdata is the register location acting as a pointer into the new space
        /// \param truncSize is the (possibly truncated) size of the register that fits the space
        /// \param isreversejustified is \b true if small variables are justified opposite of endianness
        /// \param stackGrowth is \b true if a stack implemented in this space grows in the negative direction
        /// \param isFormal is the indicator for the \b formal stack space
        protected void addSpacebase(AddrSpace basespace, string nm, VarnodeData ptrdata,
            int truncSize, bool isreversejustified, bool stackGrowth, bool isFormal)
        {
            int ind = numSpaces();

            SpacebaseSpace spc = new SpacebaseSpace(this, translate, nm,
                ind, truncSize, basespace, ptrdata.space.getDelay() + 1, isFormal);
            if (isreversejustified) {
                setReverseJustified(spc);
            }
            insertSpace(spc);
            addSpacebasePointer(spc, ptrdata, truncSize, stackGrowth);
        }

        /// Add a new region where pointers do not exist
        /// This routine is used by the initialization process to add
        /// address ranges to which there is never an (indirect) pointer
        /// Should only be called during initialization
        /// \param rng is the new range with no aliases to be added
        protected void addNoHighPtr(Sla.CORE.Range rng)
        {
            nohighptr.insertRange(rng.getSpace(), rng.getFirst(), rng.getLast());
        }

        // Factory routines for building this architecture
        /// Build the database and global scope for this executable
        /// This builds the \e universal Action for function transformation
        /// and instantiates the "decompile" root Action
        /// \param store may hold configuration information
        /// Create the database object, which currently doesn't not depend on any configuration
        /// data.  Then create the root (global) scope and attach it to the database.
        /// \param store is the storage for any configuration data
        /// \return the global Scope object
        protected virtual Scope buildDatabase(DocumentStorage store)
        {
            symboltab = new Database(this, true);
            Scope globscope = new ScopeInternal(0, "", this);
            symboltab.attachScope(globscope, null);
            return globscope;
        }

        /// \brief Build the Translator object
        /// This builds the main disassembly component for the Architecture
        /// This does \e not initially the engine for a specific processor.
        /// \param store may hold configuration information
        /// \return the Translate object
        protected abstract Translate buildTranslator(DocumentStorage store);

        /// \brief Build the LoadImage object and load the executable image
        /// \param store may hold configuration information
        protected abstract void buildLoader(DocumentStorage store);

        /// \brief Build the injection library
        /// This creates the container for p-code injections. It is initially empty.
        /// \return the PcodeInjectLibrary object
        protected abstract PcodeInjectLibrary buildPcodeInjectLibrary();

        /// \brief Build the data-type factory/container
        /// Build the TypeFactory object specific to \b this Architecture and
        /// prepopulate it with the \e core types. Core types may be pulled
        /// from the configuration information, or default core types are used.
        /// \param store contains possible configuration information
        protected abstract void buildTypegrp(DocumentStorage store);

        /// \brief Build the comment database
        /// Build the container that holds comments in \b this Architecture.
        /// \param store may hold configuration information
        protected abstract void buildCommentDB(DocumentStorage store);

        /// \brief Build the string manager
        /// Build container that holds decoded strings for \b this Architecture.
        /// \param store may hold configuration information
        protected abstract void buildStringManager(DocumentStorage store);

        /// \brief Build the constant pool
        /// Some processor models (Java byte-code) need a database of constants.
        /// The database is always built, but may remain empty.
        /// \param store may hold configuration information
        protected abstract void buildConstantPool(DocumentStorage store);

        /// Register the p-code operations
        /// This registers the OpBehavior objects for all known p-code OpCodes.
        /// The Translate and TypeFactory object should already be built.
        /// \param store may hold configuration information
        protected virtual void buildInstructions(DocumentStorage store)
        {
            TypeOp.registerInstructions(inst, types, translate);
        }

        ///< Build the Action framework
        protected virtual void buildAction(DocumentStorage store)
        {
            // Look for any additional rules
            parseExtraRules(store);
            allacts.universalAction(this);
            allacts.resetDefaults();
        }

        /// \brief Build the Context database
        /// Build the database which holds status register settings and other
        /// information that can affect disassembly depending on context.
        /// \param store may hold configuration information
        protected abstract void buildContext(DocumentStorage store);

        /// \brief Build any symbols from spec files
        /// Formal symbols described in a spec file are added to the global scope.
        /// \param store may hold symbol elements
        protected abstract void buildSymbols(DocumentStorage store);

        /// \brief Load any relevant specification files
        /// Processor/architecture specific configuration files are loaded into the XML store
        /// \param store is the document store that will hold the configuration
        protected abstract void buildSpecFile(DocumentStorage store);

        /// \brief Modify address spaces as required by \b this Architecture
        /// If spaces need to be truncated or otherwise changed from processor defaults,
        /// this routine performs the modification.
        /// \param trans is the processor disassembly object
        protected abstract void modifySpaces(Translate trans);

        /// Let components initialize after Translate is built
        protected virtual void postSpecFile()
        {
            cacheAddrSpaceProperties();
        }

        /// Figure out the processor and compiler of the target executable
        protected abstract void resolveArchitecture();

        /// Fully initialize the Translate object
        /// Once the processor is known, the Translate object can be built and
        /// fully initialized. Processor and compiler specific configuration is performed
        /// \param store will hold parsed configuration information
        protected void restoreFromSpec(DocumentStorage store)
        {
            // Once language is described we can build translator
            Translate newtrans = buildTranslator(store);
            newtrans.initialize(store);
            translate = newtrans;
            // Give architecture chance to modify spaces, before copying
            modifySpaces(newtrans);
            copySpaces(newtrans);
            insertSpace(new FspecSpace(this, translate, numSpaces()));
            insertSpace(new IopSpace(this, translate, numSpaces()));
            insertSpace(new JoinSpace(this, translate, numSpaces()));
            userops.initialize(this);
            if (translate.getAlignment() <= 8) {
                min_funcsymbol_size = translate.getAlignment();
            }
            pcodeinjectlib = buildPcodeInjectLibrary();
            parseProcessorConfig(store);
            // If no explicit formats registered, put in defaults
            newtrans.setDefaultFloatFormats();
            parseCompilerConfig(store);
            // Action stuff will go here
            buildAction(store);
        }

        /// Load info about read-only sections
        /// The LoadImage may have access information about the executables
        /// sections. Query for any read-only ranges and
        /// store this information in the property database
        protected void fillinReadOnlyFromLoader()
        {
            RangeList rangelist = new RangeList();
            // Get read only ranges
            loader.getReadonly(rangelist);
            foreach (Sla.CORE.Range iter in rangelist) {
                symboltab.setPropertyRange(Varnode.varnode_flags.@readonly, iter);
            }
        }

        /// Set up segment resolvers
        /// If any address space supports near pointers and segment operators,
        /// setup SegmentedResolver objects that can be used to recover full pointers in context.
        protected void initializeSegments()
        {
            int sz = userops.numSegmentOps();
            for (int i = 0; i < sz; ++i) {
                SegmentOp? sop = userops.getSegmentOp(i);
                if (null == sop) {
                    continue;
                }
                SegmentedResolver rsolv = new SegmentedResolver(this, sop.getSpace(), sop);
                insertResolver(sop.getSpace(), rsolv);
            }
        }

        /// Calculate some frequently used space properties and cache them
        /// Determine the minimum pointer size for the space and whether or not there are near pointers.
        /// Set up an ordered list of inferable spaces (where constant pointers can be infered).
        /// Inferable spaces include the default space and anything explicitly listed
        /// in the cspec \<global> tag that is not a register space. An initial list of potential spaces is
        /// passed in that needs to be ordered, filtered, and deduplicated.
        protected void cacheAddrSpaceProperties()
        {
            List<AddrSpace> copyList = inferPtrSpaces;
            // Make sure the default code space is present
            copyList.Add(getDefaultCodeSpace());
            // Make sure the default data space is present
            copyList.Add(getDefaultDataSpace());
            inferPtrSpaces.Clear();
            copyList.Sort(AddrSpace.compareByIndex);
            AddrSpace? lastSpace = null;
            for (int i = 0; i < copyList.Count; ++i) {
                AddrSpace spc = copyList[i];
                if (spc == lastSpace) {
                    continue;
                }
                lastSpace = spc;
                // Don't put in a register space
                if (0 == spc.getDelay()) {
                    continue;
                }
                if (spc.getType() == spacetype.IPTR_SPACEBASE) {
                    continue;
                }
                if (spc.isOtherSpace()) {
                    continue;
                }
                if (spc.isOverlay()) {
                    continue;
                }
                inferPtrSpaces.Add(spc);
            }

            int defPos = -1;
            for (int i = 0; i < inferPtrSpaces.Count; ++i) {
                AddrSpace spc = inferPtrSpaces[i];
                if (spc == getDefaultDataSpace()) {
                    // Make the default for inferring pointers the data space
                    defPos = i;
                }
                SegmentOp? segOp = getSegmentOp(spc);
                if (null != segOp) {
                    int val = segOp.getInnerSize();
                    markNearPointers(spc, val);
                }
            }
            if (defPos > 0) {
                // Make sure the default space comes first
                AddrSpace tmp = inferPtrSpaces[0];
                inferPtrSpaces[0] = inferPtrSpaces[defPos];
                inferPtrSpaces[defPos] = tmp;
            }
        }

        /// Create name alias for a ProtoModel
        /// Clone the named ProtoModel, attaching it to another name.
        /// \param aliasName is the new name to assign
        /// \param parentName is the name of the parent model
        protected void createModelAlias(string aliasName, string parentName)
        {
            ProtoModel? model;
            if (!protoModels.TryGetValue(parentName, out model)) {
                throw new CORE.LowlevelError(
                    $"Requesting non-existent prototype model: {parentName}");
            }
            if (null != model.getAliasParent()) {
                throw new CORE.LowlevelError("Cannot make alias of an alias: {parentName}");
            }
            if (protoModels.ContainsKey(aliasName)) {
                throw new CORE.LowlevelError("Duplicate ProtoModel name: {aliasName}");
            }
            protoModels[aliasName] = new ProtoModel(aliasName, model);
        }

        /// Apply processor specific configuration
        /// This looks for the \<processor_spec> tag and and sets configuration
        /// parameters based on it.
        /// \param store is the document store holding the tag
        protected void parseProcessorConfig(DocumentStorage store)
        {
            Element? el = store.getTag("processor_spec");
            if (null == el) {
                throw new CORE.LowlevelError("No processor configuration tag found");
            }
            XmlDecode decoder = new XmlDecode(this, el);

            uint elemId = decoder.openElement(ElementId.ELEM_PROCESSOR_SPEC);
            while (true) {
                uint subId = decoder.peekElement();
                if (subId == 0) {
                    break;
                }
                if (subId == ElementId.ELEM_PROGRAMCOUNTER) {
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
                else if (subId == ElementId.ELEM_VOLATILE) {
                    decodeVolatile(decoder);
                }
                else if (subId == ElementId.ELEM_INCIDENTALCOPY) {
                    decodeIncidentalCopy(decoder);
                }
                else if (subId == ElementId.ELEM_CONTEXT_DATA) {
                    context.decodeFromSpec(decoder);
                }
                else if (subId == ElementId.ELEM_JUMPASSIST) {
                    userops.decodeJumpAssist(decoder, this);
                }
                else if (subId == ElementId.ELEM_SEGMENTOP) {
                    userops.decodeSegmentOp(decoder, this);
                }
                else if (subId == ElementId.ELEM_REGISTER_DATA) {
                    decodeLaneSizes(decoder);
                }
                else if (subId == ElementId.ELEM_DATA_SPACE) {
                    elemId = decoder.openElement();
                    AddrSpace spc = decoder.readSpace(AttributeId.ATTRIB_SPACE);
                    decoder.closeElement(elemId);
                    setDefaultDataSpace(spc.getIndex());
                }
                else if (subId == ElementId.ELEM_INFERPTRBOUNDS) {
                    decodeInferPtrBounds(decoder);
                }
                else if (subId == ElementId.ELEM_SEGMENTED_ADDRESS) {
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
                else if (subId == ElementId.ELEM_DEFAULT_SYMBOLS) {
                    decoder.openElement();
                    store.registerTag(decoder.getCurrentXmlElement());
                    decoder.closeElementSkipping(subId);
                }
                else if (subId == ElementId.ELEM_DEFAULT_MEMORY_BLOCKS) {
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
                else if (subId == ElementId.ELEM_ADDRESS_SHIFT_AMOUNT) {
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
                else if (subId == ElementId.ELEM_PROPERTIES) {
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
                else {
                    throw new CORE.LowlevelError("Unknown element in <processor_spec>");
                }
            }
            decoder.closeElement(elemId);
        }

        /// Apply compiler specific configuration
        /// This looks for the \<compiler_spec> tag and sets configuration parameters based on it.
        /// \param store is the document store holding the tag
        protected void parseCompilerConfig(DocumentStorage store)
        {
            List<RangeProperties> globalRanges = new List<RangeProperties>();
            Element? el = store.getTag("compiler_spec");
            if (null == el) {
                throw new CORE.LowlevelError("No compiler configuration tag found");
            }
            XmlDecode decoder = new XmlDecode(this, el);

            uint elemId = decoder.openElement(ElementId.ELEM_COMPILER_SPEC);
            while (true) {
                uint subId = decoder.peekElement();
                if (subId == 0) {
                    break;
                }
                if (subId == ElementId.ELEM_DEFAULT_PROTO) {
                    decodeDefaultProto(decoder);
                }
                else if (subId == ElementId. ELEM_PROTOTYPE) {
                    decodeProto(decoder);
                }
                else if (subId == ElementId. ELEM_STACKPOINTER) {
                    decodeStackPointer(decoder);
                }
                else if (subId == ElementId. ELEM_RETURNADDRESS) {
                    decodeReturnAddress(decoder);
                }
                else if (subId == ElementId. ELEM_SPACEBASE) {
                    decodeSpacebase(decoder);
                }
                else if (subId == ElementId.ELEM_NOHIGHPTR) {
                    decodeNoHighPtr(decoder);
                }
                else if (subId == ElementId.ELEM_PREFERSPLIT) {
                    decodePreferSplit(decoder);
                }
                else if (subId == ElementId.ELEM_AGGRESSIVETRIM) {
                    decodeAggressiveTrim(decoder);
                }
                else if (subId == ElementId.ELEM_DATA_ORGANIZATION) {
                    types.decodeDataOrganization(decoder);
                }
                else if (subId == ElementId.ELEM_ENUM) {
                    types.parseEnumConfig(decoder);
                }
                else if (subId == ElementId.ELEM_GLOBAL) {
                    decodeGlobal(decoder, globalRanges);
                }
                else if (subId == ElementId.ELEM_SEGMENTOP) {
                    userops.decodeSegmentOp(decoder, this);
                }
                else if (subId == ElementId.ELEM_READONLY) {
                    decodeReadOnly(decoder);
                }
                else if (subId == ElementId.ELEM_CONTEXT_DATA) {
                    context.decodeFromSpec(decoder);
                }
                else if (subId == ElementId.ELEM_RESOLVEPROTOTYPE) {
                    decodeProto(decoder);
                }
                else if (subId == ElementId.ELEM_EVAL_CALLED_PROTOTYPE) {
                    decodeProtoEval(decoder);
                }
                else if (subId == ElementId.ELEM_EVAL_CURRENT_PROTOTYPE) {
                    decodeProtoEval(decoder);
                }
                else if (subId == ElementId.ELEM_CALLFIXUP) {
                    pcodeinjectlib.decodeInject(archid + " : compiler spec", "", InjectPayload.InjectionType.CALLFIXUP_TYPE, decoder);
                }
                else if (subId == ElementId.ELEM_CALLOTHERFIXUP) {
                    userops.decodeCallOtherFixup(decoder, this);
                }
                else if (subId == ElementId.ELEM_FUNCPTR) {
                    decodeFuncPtrAlign(decoder);
                }
                else if (subId == ElementId.ELEM_DEADCODEDELAY) {
                    decodeDeadcodeDelay(decoder);
                }
                else if (subId == ElementId.ELEM_INFERPTRBOUNDS) {
                    decodeInferPtrBounds(decoder);
                }
                else if (subId == ElementId.ELEM_MODELALIAS) {
                    elemId = decoder.openElement();
                    string aliasName = decoder.readString(AttributeId.ATTRIB_NAME);
                    string parentName = decoder.readString(AttributeId.ATTRIB_PARENT);
                    decoder.closeElement(elemId);
                    createModelAlias(aliasName, parentName);
                    break;
                }
            }
            decoder.closeElement(elemId);

            // Look for any user-defined configuration document
            el = store.getTag("specextensions");
            if (null != el) {
                XmlDecode decoderExt= new XmlDecode(this, el);
                elemId = decoderExt.openElement(ElementId.ELEM_SPECEXTENSIONS);
                while (true) {
                    uint subId = decoderExt.peekElement();
                    if (subId == 0) {
                        break;
                    }
                    if (subId == ElementId.ELEM_PROTOTYPE) {
                        decodeProto(decoderExt);
                    }
                    else if (subId == ElementId.ELEM_CALLFIXUP) {
                        pcodeinjectlib.decodeInject(archid + " : compiler spec", "",
                            InjectPayload.InjectionType.CALLFIXUP_TYPE, decoder);
                    }
                    else if (subId == ElementId.ELEM_CALLOTHERFIXUP) {
                        userops.decodeCallOtherFixup(decoder, this);
                    }
                    else if (subId == ElementId.ELEM_GLOBAL) {
                        decodeGlobal(decoder, globalRanges);
                    }
                }
                decoderExt.closeElement(elemId);
            }

            // <global> tags instantiate the base symbol table
            // They need to know about all spaces, so it must come
            // after parsing of <stackpointer> and <spacebase>
            for (int i = 0; i < globalRanges.Count; ++i) {
                addToGlobalScope(globalRanges[i]);
            }
            addOtherSpace();

            if (null == defaultfp) {
                if (0 >= protoModels.Count) {
                    throw new CORE.LowlevelError("No default prototype specified");
                }
                setDefaultModel(protoModels.First().Value);
            }
            // We must have a __thiscall calling convention
            if (!protoModels.ContainsKey("__thiscall")) {
                // If __thiscall doesn't exist we clone it off of the default
                createModelAlias("__thiscall", defaultfp.getName());
            }
            userops.setDefaults(this);
            initializeSegments();
            PreferSplitManager.initialize(splitrecords);
            // If no data_organization was registered, set up default values
            types.setupSizes();
        }

        ///< Apply any Rule tags
        /// Look for the \<experimental_rules> tag and create any dynamic Rule objects it specifies.
        /// \param store is the document store containing the tag
        protected void parseExtraRules(DocumentStorage store)
        {
            Element? expertag = store.getTag("experimental_rules");
            if (null != expertag) {
                XmlDecode decoder = new XmlDecode(this, expertag);
                uint elemId = decoder.openElement(ElementId.ELEM_EXPERIMENTAL_RULES);
                while (decoder.peekElement() != 0) {
                    decodeDynamicRule(decoder);
                }
                decoder.closeElement(elemId);
            }
        }

        /// Apply details of a dynamic Rule object
        /// Recover information out of a \<rule> element and build the new Rule object.
        /// \param decoder is the stream decoder
        protected void decodeDynamicRule(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_RULE);
            string rulename = string.Empty;
            string groupname = string.Empty;
            bool enabled = false;
            while (true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) {
                    break;
                }
                if (attribId == AttributeId.ATTRIB_NAME) {
                    rulename = decoder.readString();
                }
                else if (attribId == AttributeId.ATTRIB_GROUP) {
                    groupname = decoder.readString();
                }
                else if (attribId == AttributeId.ATTRIB_ENABLE) {
                    enabled = decoder.readBool();
                }
                else {
                    throw new CORE.LowlevelError(
                        "Dynamic rule tag contains illegal attribute");
                }
            }
            if (rulename.Length == 0) {
                throw new CORE.LowlevelError("Dynamic rule has no name");
            }
            if (groupname.Length == 0) {
                throw new CORE.LowlevelError("Dynamic rule has no group");
            }
            if (!enabled) {
                return;
            }
#if CPUI_RULECOMPILE
            Rule dynrule = RuleGeneric.build(rulename, groupname, el.getContent());
            extra_pool_rules.Add(dynrule);
#else
            throw new CORE.LowlevelError(
                "Dynamic rules have not been enabled for this decompiler");
#endif
            decoder.closeElement(elemId);
        }

        /// Parse a proto-type model from a stream
        /// This handles the \<prototype> and \<resolveprototype> elements. It builds the
        /// ProtoModel object based on the tag and makes it available generally to the decompiler.
        /// \param decoder is the stream decoder
        /// \return the new ProtoModel object
        protected ProtoModel decodeProto(Decoder decoder)
        {
            ProtoModel res;
            uint elemId = decoder.peekElement();
            if (elemId == ElementId.ELEM_PROTOTYPE) {
                res = new ProtoModel(this);
            }
            else if (elemId == ElementId.ELEM_RESOLVEPROTOTYPE) {
                res = new ProtoModelMerged(this);
            }
            else {
                throw new CORE.LowlevelError("Expecting <prototype> or <resolveprototype> tag");
            }

            res.decode(decoder);

            ProtoModel? other = getModel(res.getName());
            if (other != null) {
                string errMsg = $"Duplicate ProtoModel name: {res.getName()}";
                // delete res;
                throw new CORE.LowlevelError(errMsg);
            }
            protoModels[res.getName()] = res;
            return res;
        }

        /// Apply prototype evaluation configuration
        /// This decodes the \<eval_called_prototype> and \<eval_current_prototype> elements.
        /// This determines which prototype model to assume when recovering the prototype
        /// for a \e called function and the \e current function respectively.
        /// \param decoder is the stream decoder
        protected void decodeProtoEval(Decoder decoder)
        {
            uint elemId = decoder.openElement();
            string modelName = decoder.readString(AttributeId.ATTRIB_NAME);
            ProtoModel? res = getModel(modelName);
            if (null == res)
                throw new CORE.LowlevelError($"Unknown prototype model name: {modelName}");

            if (elemId == ElementId.ELEM_EVAL_CALLED_PROTOTYPE) {
                if (null != evalfp_called) {
                    throw new CORE.LowlevelError("Duplicate <eval_called_prototype> tag");
                }
                evalfp_called = res;
            }
            else {
                if (null != evalfp_current) {
                    throw new CORE.LowlevelError("Duplicate <eval_current_prototype> tag");
                }
                evalfp_current = res;
            }
            decoder.closeElement(elemId);
        }

        /// Apply default prototype model configuration
        /// There should be exactly one \<default_proto> element that specifies what the
        /// default prototype model is. This builds the ProtoModel object and sets it
        /// as the default.
        /// \param decoder is the stream decoder
        protected void decodeDefaultProto(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_DEFAULT_PROTO);
            while (decoder.peekElement() != 0) {
                if (null != defaultfp) {
                    throw new CORE.LowlevelError("More than one default prototype model");
                }
                ProtoModel model = decodeProto(decoder);
                setDefaultModel(model);
            }
            decoder.closeElement(elemId);
        }

        /// Parse information about global ranges
        /// Parse a \<global> element for child \<range> elements that will be added to the global scope.
        /// Ranges are stored in partial form so that elements can be parsed before all address spaces exist.
        /// \param decoder is the stream decoder
        /// \param rangeProps is where the partially parsed ranges are stored
        protected void decodeGlobal(Decoder decoder, List<RangeProperties> rangeProps)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_GLOBAL);
            while (decoder.peekElement() != 0) {
                RangeProperties added = new RangeProperties();
                rangeProps.Add(added);
                added.decode(decoder);
            }
            decoder.closeElement(elemId);
        }

        /// Add a memory range to the set of addresses considered \e global
        /// Add a memory range parse from a \<global> tag to the global scope.
        /// Varnodes in this region will be assumed to be global variables.
        /// \param props is information about a specific range
        protected void addToGlobalScope(RangeProperties props)
        {
            Scope scope = symboltab.getGlobalScope() ?? throw new ApplicationException();
            Sla.CORE.Range range = new Sla.CORE.Range(props, this);
            AddrSpace spc = range.getSpace();
            inferPtrSpaces.Add(spc);
            symboltab.addRange(scope, spc, range.getFirst(), range.getLast());
            if (range.getSpace().isOverlayBase()) {
                // If the address space is overlayed
                // We need to duplicate the range being marked as global into the overlay space(s)
                int num = numSpaces();
                for (int i = 0; i < num; ++i) {
                    AddrSpace? ospc = getSpace(i);
                    if (null == ospc || !ospc.isOverlay()) {
                        continue;
                    }
                    if (ospc.getContain() != range.getSpace()) {
                        continue;
                    }
                    symboltab.addRange(scope, ospc, range.getFirst(), range.getLast());
                }
            }
        }

        /// Add OTHER space and all of its overlays to the symboltab
        //explictly add the OTHER space and any overlays to the global scope
        protected void addOtherSpace()
        {
            Scope scope = symboltab.getGlobalScope() ?? throw new ApplicationException();
            AddrSpace otherSpace = getSpaceByName(OtherSpace.NAME)
                ?? throw new ApplicationException();
            symboltab.addRange(scope, otherSpace, 0, otherSpace.getHighest());
            if (otherSpace.isOverlayBase()) {
                int num = numSpaces();
                for (int i = 0; i < num; ++i) {
                    AddrSpace ospc = getSpace(i);
                    if (!ospc.isOverlay()) {
                        continue;
                    }
                    if (ospc.getContain() != otherSpace) {
                        continue;
                    }
                    symboltab.addRange(scope, ospc, 0, otherSpace.getHighest());
                }
            }
        }

        /// Apply read-only region configuration
        /// This applies info from a \<readonly> element marking a specific region
        /// of the executable as \e read-only.
        /// \param decoder is the stream decoder
        protected void decodeReadOnly(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_READONLY);
            while (decoder.peekElement() != 0) {
                Sla.CORE.Range range = new Sla.CORE.Range();
                range.decode(decoder);
                symboltab.setPropertyRange(Varnode.varnode_flags.@readonly, range);
            }
            decoder.closeElement(elemId);
        }

        /// Apply volatile region configuration
        /// This applies info from a \<volatile> element marking specific regions
        /// of the executable as holding \e volatile memory or registers.
        /// \param decoder is the stream decoder
        protected void decodeVolatile(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_VOLATILE);
            userops.decodeVolatile(decoder, this);
            while (decoder.peekElement() != 0) {
                Sla.CORE.Range range = new Sla.CORE.Range();
                // Tag itself is range
                range.decode(decoder);
                symboltab.setPropertyRange(Varnode.varnode_flags.volatil, range);
            }
            decoder.closeElement(elemId);
        }

        /// Apply return address configuration
        /// This applies info from \<returnaddress> element and sets the default
        /// storage location for the \e return \e address of a function.
        /// \param decoder is the stream decoder
        protected void decodeReturnAddress(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_RETURNADDRESS);
            uint subId = decoder.peekElement();
            if (subId != 0) {
                if (null != defaultReturnAddr.space) {
                    throw new CORE.LowlevelError("Multiple <returnaddress> tags in .cspec");
                }
                defaultReturnAddr = VarnodeData.decode(decoder);
            }
            decoder.closeElement(elemId);
        }

        /// Apply incidental copy configuration
        /// Apply information from an \<incidentalcopy> element, which marks a set of addresses
        /// as being copied to incidentally. This allows the decompiler to ignore certain side-effects.
        /// \param decoder is the stream decoder
        protected void decodeIncidentalCopy(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_INCIDENTALCOPY);
            while (decoder.peekElement() != 0) {
                VarnodeData vdata = VarnodeData.decode(decoder);
                Sla.CORE.Range range = new Sla.CORE.Range(vdata.space, vdata.offset,
                    vdata.offset+vdata.size - 1);
                symboltab.setPropertyRange(Varnode.varnode_flags.incidental_copy, range);
            }
            decoder.closeElement(elemId);
        }

        /// Apply lane size configuration
        /// Look for \<register> elements that have a \e vector_lane_size attribute.
        /// Record these so that the decompiler can split large registers into appropriate lane size pieces.
        /// \param decoder is the stream decoder
        protected void decodeLaneSizes(Decoder decoder)
        {
            List<uint> maskList = new List<uint>();
            // Only allocate once
            LanedRegister lanedRegister = new LanedRegister();

            uint elemId = decoder.openElement(ElementId.ELEM_REGISTER_DATA);
            while (decoder.peekElement() != 0) {
                if (lanedRegister.decode(decoder)) {
                    int sizeIndex = lanedRegister.getWholeSize();
                    while (maskList.Count <= sizeIndex) {
                        maskList.Add(0);
                    }
                    maskList[sizeIndex] |= lanedRegister.getSizeBitMask();
                }
            }
            decoder.closeElement(elemId);
            lanerecords.Clear();
            for (int i = 0; i < maskList.Count; ++i) {
                if (maskList[i] == 0) {
                    continue;
                }
                lanerecords.Add(new LanedRegister(i, maskList[i]));
            }
        }

        /// Apply stack pointer configuration
        /// Create a stack space and a stack-pointer register from a \<stackpointer> element
        /// \param decoder is the stream decoder
        protected void decodeStackPointer(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_STACKPOINTER);
            string registerName;
            // Default stack growth is in negative direction
            bool stackGrowth = true;
            bool isreversejustify = false;
            AddrSpace? basespace = null;
            while (true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) {
                    break;
                }
                if (attribId == AttributeId.ATTRIB_REVERSEJUSTIFY) {
                    isreversejustify = decoder.readBool();
                }
                else if (attribId == AttributeId.ATTRIB_GROWTH) {
                    stackGrowth = decoder.readString() == "negative";
                }
                else if (attribId == AttributeId.ATTRIB_SPACE) {
                    basespace = decoder.readSpace();
                }
                else if (attribId == AttributeId.ATTRIB_REGISTER) {
                    registerName = decoder.readString();
                }
            }

            if (null == basespace) {
                throw new CORE.LowlevelError(
                    $"{ElementId.ELEM_STACKPOINTER.getName()} element missing \"space\" attribute");
            }

            VarnodeData point = translate.getRegister(registerName);
            decoder.closeElement(elemId);

            // If creating a stackpointer to a truncated space, make sure to truncate the
            // stackpointer
            int truncSize = (int)point.size;
            if (basespace.isTruncated() && (point.size > basespace.getAddrSize())) {
                truncSize = (int)basespace.getAddrSize();
            }
            // Create the "official" stackpointer
            addSpacebase(basespace, "stack", point, truncSize, isreversejustify,
                stackGrowth, true);
        }

        /// Apply dead-code delay configuration
        /// Manually alter the dead-code delay for a specific address space,
        /// based on a \<deadcodedelay> element.
        /// \param decoder is the stream decoder
        protected void decodeDeadcodeDelay(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_DEADCODEDELAY);
            AddrSpace spc = decoder.readSpace(AttributeId.ATTRIB_SPACE);
            int delay = (int)decoder.readSignedInteger(AttributeId.ATTRIB_DELAY);
            if (delay >= 0) {
                setDeadcodeDelay(spc, delay);
            }
            else {
                throw new CORE.LowlevelError("Bad <deadcodedelay> tag");
            }
            decoder.closeElement(elemId);
        }

        /// Apply pointer inference bounds
        /// Alter the range of addresses for which a pointer is allowed to be inferred.
        protected void decodeInferPtrBounds(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_INFERPTRBOUNDS);
            while (decoder.peekElement() != 0) {
                Sla.CORE.Range range = new Sla.CORE.Range();
                range.decode(decoder);
                setInferPtrBounds(range);
            }
            decoder.closeElement(elemId);
        }

        /// Apply function pointer alignment configuration
        /// Pull information from a \<funcptr> element. Turn on alignment analysis of
        /// function pointers, some architectures have aligned function pointers
        /// and encode extra information in the unused bits.
        /// \param decoder is the stream decoder
        protected void decodeFuncPtrAlign(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_FUNCPTR);
            int align = (int)decoder.readSignedInteger(AttributeId.ATTRIB_ALIGN);
            decoder.closeElement(elemId);

            if (align == 0) {
                // No alignment
                funcptr_align = 0;
                return;
            }
            int bits = 0;
            while ((align & 1) == 0) {
                // Find position of first 1 bit
                bits += 1;
                align >>= 1;
            }
            funcptr_align = bits;
        }

        /// Create an additional indexed space
        /// Designate a new index register and create a new address space associated with it,
        /// based on a \<spacebase> element.
        /// \param decoder is the stream decoder
        protected void decodeSpacebase(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_SPACEBASE);
            string nameString = decoder.readString(AttributeId.ATTRIB_NAME);
            string registerName = decoder.readString(AttributeId.ATTRIB_REGISTER);
            AddrSpace basespace = decoder.readSpace(AttributeId.ATTRIB_SPACE);
            decoder.closeElement(elemId);
            VarnodeData point = translate.getRegister(registerName);
            addSpacebase(basespace, nameString, point, (int)point.size, false, false, false);
        }

        /// Apply memory alias configuration
        /// Configure memory based on a \<nohighptr> element. Mark specific address ranges
        /// to indicate the decompiler will not encounter pointers (aliases) into the range.
        /// \param decoder is the stream decoder
        protected void decodeNoHighPtr(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_NOHIGHPTR);
            while (decoder.peekElement() != 0) {
                // Iterate over every range tag in the list
                Sla.CORE.Range range = new Sla.CORE.Range();
                range.decode(decoder);
                addNoHighPtr(range);
            }
            decoder.closeElement(elemId);
        }

        /// Designate registers to be split
        /// Configure registers based on a \<prefersplit> element. Mark specific varnodes that
        /// the decompiler should automatically split when it first sees them.
        /// \param decoder is the stream decoder
        protected void decodePreferSplit(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_PREFERSPLIT);
            string style = decoder.readString(AttributeId.ATTRIB_STYLE);
            if (style != "inhalf") {
                throw new CORE.LowlevelError($"Unknown prefersplit style: {style}");
            }

            while (decoder.peekElement() != 0) {
                PreferSplitRecord record = new PreferSplitRecord();
                splitrecords.Add(record);
                record.storage = VarnodeData.decode(decoder);
                record.splitoffset = record.storage.size() / 2;
            }
            decoder.closeElement(elemId);
        }

        /// Designate how to trim extension p-code ops
        /// Configure, based on the \<aggressivetrim> element, how aggressively the
        /// decompiler will remove extension operations.
        /// \param decoder is the stream decoder
        protected void decodeAggressiveTrim(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_AGGRESSIVETRIM);
            while (true) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) {
                    break;
                }
                if (attribId == AttributeId.ATTRIB_SIGNEXT) {
                    aggressive_ext_trim = decoder.readBool();
                }
            }
            decoder.closeElement(elemId);
        }
    }
}
