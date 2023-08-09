using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;

namespace Sla.DECCORE
{
    /// \brief A \b prototype \b model: a model for passing parameters between functions
    ///
    /// This encompasses both input parameters and return values. It attempts to
    /// describe the ABI, Application Binary Interface, of the processor or compiler.
    /// Any number of function prototypes (FuncProto) can be implemented under a
    /// \b prototype \b model, which represents a static rule set the compiler uses
    /// to decide:
    ///   - Storage locations for input parameters
    ///   - Storage locations for return values
    ///   - Expected side-effects of a function on other (non-parameter) registers and storage locations
    ///   - Behavior of the stack and the stack pointer across function calls
    ///
    /// Major analysis concerns are:
    ///   - Recovering function prototypes from data-flow information: deriveInputMap() and deriveOutputMap()
    ///   - Calculating parameter storage locations given a function prototype: assignParameterStorage()
    ///   - Behavior of data-flow around call sites
    ///
    /// A prototype model supports the concept of \b extrapop, which is defined as the change in
    /// value of the stack pointer (or the number of bytes popped from the stack) across a call.
    /// This value is calculated starting from the point of the p-code CALL or CALLIND op, when the
    /// stack parameters have already been pushed by the calling function. So \e extrapop only reflects
    /// changes made by the callee.
    internal class ProtoModel
    {
        /// Reserved extrapop value meaning the function's \e extrapop is unknown
        internal const int extrapop_unknown = 0x8000;

        // friend class ProtoModelMerged;
        /// The Architecture owning this prototype model
        private Architecture glb;
        /// Name of the model
        private string name;
        /// Extra bytes popped from stack
        private int extrapop;
        /// Resource model for input parameters
        private ParamList input;
        /// Resource model for output parameters
        private ParamList output;
        /// The model \b this is a copy of
        private ProtoModel compatModel;
        /// List of side-effects
        private List<EffectRecord> effectlist;
        /// Storage locations potentially carrying \e trash values
        private List<VarnodeData> likelytrash;
        /// Id of injection to perform at beginning of function (-1 means not used)
        private int injectUponEntry;
        /// Id of injection to perform after a call to this function (-1 means not used)
        private int injectUponReturn;
        /// Memory range(s) of space-based locals
        private RangeList localrange;
        /// Memory range(s) of space-based parameters
        private RangeList paramrange;
        /// True if stack parameters have (normal) low address to high address ordering
        private bool stackgrowsnegative;
        /// True if this model has a \b this parameter (auto-parameter)
        private bool hasThis;
        /// True if this model is a constructor for a particular object
        private bool isConstruct;
        /// True if this model should be printed as part of function declarations
        private bool isPrinted;

        /// Set the default stack range used for local variables
        private void defaultLocalRange()
        {
            AddrSpace* spc = glb.getStackSpace();
            ulong first, last;

            if (stackgrowsnegative)
            {   // This the normal stack convention
                // Default locals are negative offsets off the stack
                last = spc.getHighest();
                if (spc.getAddrSize() >= 4)
                    first = last - 999999;
                else if (spc.getAddrSize() >= 2)
                    first = last - 9999;
                else
                    first = last - 99;
                localrange.insertRange(spc, first, last);
            }
            else
            {           // This is the flipped stack convention
                first = 0;
                if (spc.getAddrSize() >= 4)
                    last = 999999;
                else if (spc.getAddrSize() >= 2)
                    last = 9999;
                else
                    last = 99;
                localrange.insertRange(spc, first, last);
            }
        }

        /// Set the default stack range used for input parameters
        private void defaultParamRange()
        {
            AddrSpace* spc = glb.getStackSpace();
            ulong first, last;

            if (stackgrowsnegative)
            {   // This the normal stack convention
                // Default parameters are positive offsets off the stack
                first = 0;
                if (spc.getAddrSize() >= 4)
                    last = 511;
                else if (spc.getAddrSize() >= 2)
                    last = 255;
                else
                    last = 15;
                paramrange.insertRange(spc, first, last);
            }
            else
            {           // This is the flipped stack convention
                last = spc.getHighest();
                if (spc.getAddrSize() >= 4)
                    first = last - 511;
                else if (spc.getAddrSize() >= 2)
                    first = last - 255;
                else
                    first = last - 15;
                paramrange.insertRange(spc, first, last); // Parameters are negative offsets
            }
        }

        /// Establish the main resource lists for input and output parameters.
        /// Generate derived ParamList objects based on a given strategy
        /// \param strategy is the resource \e strategy: currently "standard" or "register"
        private void buildParamList(string strategy)
        {
            if ((strategy == "") || (strategy == "standard"))
            {
                input = new ParamListStandard();
                output = new ParamListStandardOut();
            }
            else if (strategy == "register")
            {
                input = new ParamListRegister();
                output = new ParamListRegisterOut();
            }
            else
                throw new LowlevelError("Unknown strategy type: " + strategy);
        }

        /// Constructor for use with decode()
        /// \param g is the Architecture that will own the new prototype model
        public ProtoModel(Architecture g)
        {
            glb = g;
            input = (ParamList*)0;
            output = (ParamList*)0;
            compatModel = (ProtoModel)null;
            extrapop = 0;
            injectUponEntry = -1;
            injectUponReturn = -1;
            stackgrowsnegative = true;  // Normal stack parameter ordering
            hasThis = false;
            isConstruct = false;
            isPrinted = true;
            defaultLocalRange();
            defaultParamRange();
        }

        /// Copy constructor changing the name
        /// Everything is copied from the given prototype model except the name
        /// \param nm is the new name for \b this copy
        /// \param op2 is the prototype model to copy
        public ProtoModel(string nm, ProtoModel op2)
        {
            glb = op2.glb;
            name = nm;
            isPrinted = true;       // Don't inherit. Always print unless setPrintInDecl called explicitly
            extrapop = op2.extrapop;
            if (op2.input != (ParamList*)0)
                input = op2.input.clone();
            else
                input = (ParamList*)0;
            if (op2.output != (ParamList*)0)
                output = op2.output.clone();
            else
                output = (ParamList*)0;

            effectlist = op2.effectlist;
            likelytrash = op2.likelytrash;

            injectUponEntry = op2.injectUponEntry;
            injectUponReturn = op2.injectUponReturn;
            localrange = op2.localrange;
            paramrange = op2.paramrange;
            stackgrowsnegative = op2.stackgrowsnegative;
            hasThis = op2.hasThis;
            isConstruct = op2.isConstruct;
            if (name == "__thiscall")
                hasThis = true;
            compatModel = &op2;
        }

        ~ProtoModel()
        {
            if (input != (ParamList*)0)
                delete input;
            if (output != (ParamList*)0)
                delete output;
        }

        /// Get the name of the prototype model
        public string getName() => name;

        /// Get the owning Architecture
        public Architecture getArch() => glb;

        /// Return \e model \b this is an alias of (or null)
        public ProtoModel getAliasParent() => compatModel;

        /// Determine side-effect of \b this on the given memory range
        /// The model is searched for an EffectRecord matching the given range
        /// and the effect type is returned. If there is no EffectRecord or the
        /// effect generally isn't known,  EffectRecord::unknown_effect is returned.
        /// \param addr is the starting address of the given memory range
        /// \param size is the number of bytes in the given range
        /// \return the EffectRecord type
        public uint hasEffect(Address addr, int size)
        {
            return lookupEffect(effectlist, addr, size);
        }

        /// Get the stack-pointer \e extrapop for \b this model
        public int getExtraPop() => extrapop;

        /// Set the stack-pointer \e extrapop
        public void setExtraPop(int ep)
        {
            extrapop = ep;
        }

        /// Get the inject \e uponentry id
        public int getInjectUponEntry() => injectUponEntry;

        /// Get the inject \e uponreturn id
        public int getInjectUponReturn() => injectUponReturn;

        /// Return \b true if other given model can be substituted for \b this
        /// Test whether one ProtoModel can substituted for another during FuncCallSpecs::deindirect
        /// Currently this can only happen if one model is a copy of the other except for the
        /// hasThis boolean property.
        /// \param op2 is the other ProtoModel to compare with \b this
        /// \return \b true if the two models are compatible
        public bool isCompatible(ProtoModel op2)
        {
            if (this == op2 || compatModel == op2 || op2.compatModel == this)
                return true;
            return false;
        }

        /// \brief Given a list of input \e trials, derive the most likely input prototype
        /// Trials are sorted and marked as \e used or not.
        /// \param active is the collection of Varnode input trials
        public void deriveInputMap(ParamActive active) 
        {
            input.fillinMap(active);
        }

        /// \brief Given a list of output \e trials, derive the most likely output prototype
        /// One trial (at most) is marked \e used and moved to the front of the list
        /// \param active is the collection of output trials
        public void deriveOutputMap(ParamActive active)
        {
            output.fillinMap(active);
        }

        /// \brief Calculate input and output storage locations given a function prototype
        ///
        /// The data-types of the function prototype are passed in as an ordered list, with the
        /// first data-type corresponding to the \e return \e value and all remaining
        /// data-types corresponding to the input parameters.  Based on \b this model, a storage location
        /// is selected for each (input and output) parameter and passed back to the caller.
        /// The passed back storage locations are ordered similarly, with the output storage
        /// as the first entry.  The model has the option of inserting a \e hidden return value
        /// pointer in the input storage locations.
        ///
        /// A \b void return type is indicated by the formal type_metatype.TYPE_VOID in the (either) list.
        /// If the model can't map the specific output prototype, the caller has the option of whether
        /// an exception (ParamUnassignedError) is thrown.  If they choose not to throw,
        /// the unmapped return value is assumed to be \e void.
        /// \param typelist is the list of data-types from the function prototype
        /// \param res will hold the storage locations for each parameter
        /// \param ignoreOutputError is \b true if problems assigning the output parameter are ignored
        public void assignParameterStorage(List<Datatype> typelist, List<ParameterPieces> res,
            bool ignoreOutputError)
        {
            if (ignoreOutputError)
            {
                try
                {
                    output.assignMap(typelist, *glb.types, res);
                }
                catch (ParamUnassignedError err) {
                    res.clear();
                    res.emplace_back();
                    // leave address undefined
                    res.GetLastItem().flags = 0;
                    res.GetLastItem().type = glb.types.getTypeVoid();
                }
            }
            else
            {
                output.assignMap(typelist, *glb.types, res);
            }
            input.assignMap(typelist, *glb.types, res);
        }

        /// \brief Check if the given two input storage locations can represent a single logical parameter
        ///
        /// Within the conventions of this model, do the two (hi/lo) locations represent
        /// consecutive input parameter locations that can be replaced by a single logical parameter.
        /// \param hiaddr is the address of the most significant part of the value
        /// \param hisize is the size of the most significant part in bytes
        /// \param loaddr is the address of the least significant part of the value
        /// \param losize is the size of the least significant part in bytes
        /// \return \b true if the two pieces can be joined
        public bool checkInputJoin(Address hiaddr, int hisize, Address loaddr, int losize)
        {
            return input.checkJoin(hiaddr, hisize, loaddr, losize);
        }

        /// \brief Check if the given two output storage locations can represent a single logical return value
        ///
        /// Within the conventions of this model, do the two (hi/lo) locations represent
        /// consecutive locations that can be replaced by a single logical return value.
        /// \param hiaddr is the address of the most significant part of the value
        /// \param hisize is the size of the most significant part in bytes
        /// \param loaddr is the address of the least significant part of the value
        /// \param losize is the size of the least significant part in bytes
        /// \return \b true if the two pieces can be joined
        public bool checkOutputJoin(Address hiaddr, int hisize, Address loaddr, int losize)
        {
            return output.checkJoin(hiaddr, hisize, loaddr, losize);
        }

        /// \brief Check if it makes sense to split a single storage location into two input parameters
        ///
        /// A storage location and split point is provided, implying two new storage locations. Does
        /// \b this model allow these locations to be considered separate parameters.
        /// \param loc is the starting address of provided storage location
        /// \param size is the size of the location in bytes
        /// \param splitpoint is the number of bytes to consider in the first (in address order) piece
        /// \return \b true if the storage location can be split
        public bool checkInputSplit(Address loc, int size, int splitpoint)
        {
            return input.checkSplit(loc, size, splitpoint);
        }

        public RangeList getLocalRange() => localrange; ///< Get the range of (possible) local stack variables

        public RangeList getParamRange() => paramrange; ///< Get the range of (possible) stack parameters

        public IEnumerator<EffectRecord> effectBegin() => effectlist.begin(); ///< Get an iterator to the first EffectRecord

        public IEnumerator<EffectRecord> effectEnd() => effectlist.end(); ///< Get an iterator to the last EffectRecord

        public IEnumerator<VarnodeData> trashBegin() => likelytrash.begin(); ///< Get an iterator to the first \e likelytrash

        public IEnumerator<VarnodeData> trashEnd() => likelytrash.end(); ///< Get an iterator to the last \e likelytrash

        /// \brief Characterize whether the given range overlaps parameter storage
        ///
        /// Does the range naturally fit inside a potential parameter entry from this model or does
        /// it contain a parameter entry. Return one of four values indicating this characterization:
        ///   - no_containment - there is no containment between the range and any parameter in this list
        ///   - contains_unjustified - at least one parameter contains the range
        ///   - contains_justified - at least one parameter contains this range as its least significant bytes
        ///   - contained_by - no parameter contains this range, but the range contains at least one parameter
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the given range
        /// \return the characterization code
        public int characterizeAsInputParam(Address loc, int size)
        {
            return input.characterizeAsParam(loc, size);
        }

        /// \brief Characterize whether the given range overlaps output storage
        ///
        /// Does the range naturally fit inside a potential output entry from this model or does
        /// it contain an output entry. Return one of four values indicating this characterization:
        ///   - no_containment - there is no containment between the range and any parameter in this list
        ///   - contains_unjustified - at least one parameter contains the range
        ///   - contains_justified - at least one parameter contains this range as its least significant bytes
        ///   - contained_by - no parameter contains this range, but the range contains at least one parameter
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the given range
        /// \return the characterization code
        public int characterizeAsOutput(Address loc, int size)
        {
            return output.characterizeAsParam(loc, size);
        }

        /// \brief Does the given storage location make sense as an input parameter
        ///
        /// Within \b this model, decide if the storage location can be considered an input parameter.
        /// \param loc is the starting address of the storage location
        /// \param size is the number of bytes in the storage location
        /// \return \b true if the location can be a parameter
        public bool possibleInputParam(Address loc, int size)
        {
            return input.possibleParam(loc, size);
        }

        /// \brief Does the given storage location make sense as a return value
        ///
        /// Within \b this model, decide if the storage location can be considered an output parameter.
        /// \param loc is the starting address of the storage location
        /// \param size is the number of bytes in the storage location
        /// \return \b true if the location can be a parameter
        public bool possibleOutputParam(Address loc, int size)
        {
            return output.possibleParam(loc, size);
        }

        /// \brief Pass-back the slot and slot size for the given storage location as an input parameter
        ///
        /// This checks if the given storage location acts as an input parameter in \b this model and
        /// passes back the number of slots that it occupies.
        /// \param loc is the starting address of the storage location
        /// \param size is the number of bytes in the storage location
        /// \param slot if the \e slot number to pass back
        /// \param slotsize is the number of consumed slots to pass back
        /// \return \b true if the location can be a parameter
        public bool possibleInputParamWithSlot(Address loc, int size, int slot, int slotsize)
        {
            return input.possibleParamWithSlot(loc, size, slot, slotsize);
        }

        /// \brief Pass-back the slot and slot size for the given storage location as a return value
        ///
        /// This checks if the given storage location acts as an output parameter in \b this model and
        /// passes back the number of slots that it occupies.
        /// \param loc is the starting address of the storage location
        /// \param size is the number of bytes in the storage location
        /// \param slot if the \e slot number to pass back
        /// \param slotsize is the number of consumed slots to pass back
        /// \return \b true if the location can be a parameter
        public bool possibleOutputParamWithSlot(Address loc, int size, int slot, int slotsize)
        {
            return output.possibleParamWithSlot(loc, size, slot, slotsize);
        }

        /// \brief Check if the given storage location looks like an \e unjustified input parameter
        ///
        /// The storage for a value may be contained in a normal parameter location but be
        /// unjustified within that container, i.e. the least significant bytes are not being used.
        /// If this is the case, pass back the full parameter location and return \b true.
        /// \param loc is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the full parameter storage to pass back
        /// \return \b true if the given storage is unjustified within its parameter container
        public bool unjustifiedInputParam(Address loc, int size, VarnodeData res)
        {
            return input.unjustifiedContainer(loc, size, res);
        }

        /// \brief Get the type of extension and containing input parameter for the given storage
        ///
        /// If the given storage is properly contained within a normal parameter and the model
        /// typically extends a small value into the full container, pass back the full container
        /// and the type of extension.
        /// \param addr is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the parameter storage to pass back
        /// \return the extension operator (INT_ZEXT INT_SEXT) or INT_COPY if there is no extension.
        /// INT_PIECE indicates the extension is determined by the specific prototype.
        public OpCode assumedInputExtension(Address addr, int size, VarnodeData res)
        {
            return input.assumedExtension(addr, size, res);
        }

        /// \brief Get the type of extension and containing return value location for the given storage
        ///
        /// If the given storage is properly contained within a normal return value location and the model
        /// typically extends a small value into the full container, pass back the full container
        /// and the type of extension.
        /// \param addr is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the parameter storage to pass back
        /// \return the extension operator (INT_ZEXT INT_SEXT) or INT_COPY if there is no extension.
        /// INT_PIECE indicates the extension is determined by the specific prototype.
        public OpCode assumedOutputExtension(Address addr, int size, VarnodeData res)
        {
            return output.assumedExtension(addr, size, res);
        }

        /// \brief Pass-back the biggest input parameter contained within the given range
        ///
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \param res will hold the parameter storage description being passed back
        /// \return \b true if there is at least one parameter contained in the range
        public bool getBiggestContainedInputParam(Address loc, int size, VarnodeData res)
        {
            return input.getBiggestContainedParam(loc, size, res);
        }

        /// \brief Pass-back the biggest possible output parameter contained within the given range
        ///
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \param res will hold the storage description being passed back
        /// \return \b true if there is at least one possible output parameter contained in the range
        public bool getBiggestContainedOutput(Address loc, int size, VarnodeData res)
        {
            return output.getBiggestContainedParam(loc, size, res);
        }

        public AddrSpace getSpacebase() => input.getSpacebase(); ///< Get the stack space associated with \b this model

        public bool isStackGrowsNegative() => stackgrowsnegative; ///< Return \b true if the stack \e grows toward smaller addresses

        public bool hasThisPointer() => hasThis; ///< Is \b this a model for (non-static) class methods

        public bool isConstructor() => isConstruct; ///< Is \b this model for class constructors

        public bool printInDecl() => isPrinted; ///< Return \b true if name should be printed in function declarations

        public void setPrintInDecl(bool val)
        {
            isPrinted = val;
        }    ///< Set whether \b this name should be printed in function declarations

        /// \brief Return the maximum heritage delay across all possible input parameters
        ///
        /// Depending on the address space, data-flow for a parameter may not be available until
        /// extra transform passes have completed. This method returns the number of passes
        /// that must occur before we can guarantee that all parameters have data-flow info.
        /// \return the maximum number of passes across all input parameters in \b this model
        public int getMaxInputDelay() => input.getMaxDelay();

        /// \brief Return the maximum heritage delay across all possible return values
        ///
        /// Depending on the address space, data-flow for a parameter may not be available until
        /// extra transform passes have completed. This method returns the number of passes
        /// that must occur before we can guarantee that any return value has data-flow info.
        /// \return the maximum number of passes across all output parameters in \b this model
        public int getMaxOutputDelay() => output.getMaxDelay();

        /// Is \b this a merged prototype model
        public virtual bool isMerged() => false;

        /// Is \b this an unrecognized prototype model
        public virtual bool isUnknown() => false;

        /// Restore \b this model from a stream
        /// Parse details about \b this model from a \<prototype> element
        /// \param decoder is the stream decoder
        public virtual void decode(Sla.CORE.Decoder decoder)
        {
            bool sawlocalrange = false;
            bool sawparamrange = false;
            bool sawretaddr = false;
            stackgrowsnegative = true;  // Default growth direction
            AddrSpace? stackspc = glb.getStackSpace();
            if (stackspc != (AddrSpace)null)
                stackgrowsnegative = stackspc.stackGrowsNegative();    // Get growth boolean from stack space itself
            string strategystring;
            localrange.clear();
            paramrange.clear();
            extrapop = -300;
            hasThis = false;
            isConstruct = false;
            isPrinted = true;
            effectlist.Clear();
            injectUponEntry = -1;
            injectUponReturn = -1;
            likelytrash.Clear();
            uint elemId = decoder.openElement(ElementId.ELEM_PROTOTYPE);
            while(true)
            {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_NAME)
                    name = decoder.readString();
                else if (attribId == AttributeId.ATTRIB_EXTRAPOP) {
                    extrapop = decoder.readSignedIntegerExpectString("unknown", extrapop_unknown);
                }
                else if (attribId == AttributeId.ATTRIB_STACKSHIFT) {
                    // Allow this attribute for backward compatibility
                }
                else if (attribId == AttributeId.ATTRIB_STRATEGY) {
                    strategystring = decoder.readString();
                }
                else if (attribId == AttributeId.ATTRIB_HASTHIS) {
                    hasThis = decoder.readBool();
                }
                else if (attribId == AttributeId.ATTRIB_CONSTRUCTOR) {
                    isConstruct = decoder.readBool();
                }
                else
                    throw new LowlevelError("Unknown prototype attribute");
            }
            if (name == "__thiscall")
                hasThis = true;
            if (extrapop == -300)
                throw new LowlevelError("Missing prototype attributes");

            buildParamList(strategystring); // Allocate input and output ParamLists
            while(true)
            {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_INPUT) {
                    input.decode(decoder, effectlist, stackgrowsnegative);
                    if (stackspc != (AddrSpace)null) {
                        input.getRangeList(stackspc, paramrange);
                        if (!paramrange.empty())
                            sawparamrange = true;
                    }
                }
                else if (subId == ElementId.ELEM_OUTPUT) {
                    output.decode(decoder, effectlist, stackgrowsnegative);
                }
                else if (subId == ElementId.ELEM_UNAFFECTED) {
                    decoder.openElement();
                    while (decoder.peekElement() != 0) {
                        effectlist.emplace_back();
                        effectlist.GetLastItem().decode(EffectRecord.EffectType.unaffected, decoder);
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_KILLEDBYCALL) {
                    decoder.openElement();
                    while (decoder.peekElement() != 0)
                    {
                        effectlist.emplace_back();
                        effectlist.GetLastItem().decode(EffectRecord::killedbycall, decoder);
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_RETURNADDRESS) {
                    decoder.openElement();
                    while (decoder.peekElement() != 0) {
                        effectlist.emplace_back();
                        effectlist.GetLastItem().decode(EffectRecord.EffectType.return_address, decoder);
                    }
                    decoder.closeElement(subId);
                    sawretaddr = true;
                }
                else if (subId == ElementId.ELEM_LOCALRANGE) {
                    sawlocalrange = true;
                    decoder.openElement();
                    while (decoder.peekElement() != 0) {
                        Sla.CORE.Range range = new CORE.Range();
                        range.decode(decoder);
                        localrange.insertRange(range.getSpace(), range.getFirst(), range.getLast());
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_PARAMRANGE) {
                    sawparamrange = true;
                    decoder.openElement();
                    while (decoder.peekElement() != 0) {
                        Sla.CORE.Range range = new CORE.Range();
                        range.decode(decoder);
                        paramrange.insertRange(range.getSpace(), range.getFirst(), range.getLast());
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_LIKELYTRASH) {
                    decoder.openElement();
                    while (decoder.peekElement() != 0) {
                        likelytrash.Add(VarnodeData.decode(decoder));
                    }
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_PCODE) {
                    int injectId = glb.pcodeinjectlib.decodeInject("Protomodel : " + name, name,
                        InjectPayload.InjectionType.CALLMECHANISM_TYPE, decoder);
                    InjectPayload payload = glb.pcodeinjectlib.getPayload(injectId);
                    if (-1 != payload.getName().IndexOf("uponentry"))
                        injectUponEntry = injectId;
                    else
                        injectUponReturn = injectId;
                }
                else
                    throw new LowlevelError("Unknown element in prototype");
            }
            decoder.closeElement(elemId);
            if (!sawretaddr && (glb.defaultReturnAddr.space != (AddrSpace)null)) {
                // Provide the default return address, if there isn't a specific one for the model
                effectlist.Add(new EffectRecord(glb.defaultReturnAddr, EffectRecord.EffectType.return_address));
            }
            effectlist.Sort(EffectRecord::compareByAddress);
            likelytrash.Sort();
            if (!sawlocalrange)
                defaultLocalRange();
            if (!sawparamrange)
                defaultParamRange();
        }

        /// \brief Look up an effect from the given EffectRecord list
        /// If a given memory range matches an EffectRecord, return the effect type.
        /// Otherwise return EffectRecord::unknown_effect
        /// \param efflist is the list of EffectRecords which must be sorted
        /// \param addr is the starting address of the given memory range
        /// \param size is the number of bytes in the memory range
        /// \return the EffectRecord type
        public static uint lookupEffect(List<EffectRecord> efflist, Address addr, int size)
        {
            // Unique is always local to function
            if (addr.getSpace().getType() == spacetype.IPTR_INTERNAL) return EffectRecord.EffectType.unaffected;

            EffectRecord cur(addr, size);

            List<EffectRecord>::const_iterator iter;

            iter = upper_bound(efflist.begin(), efflist.end(), cur, EffectRecord::compareByAddress);
            // First element greater than cur  (address must be greater)
            // go back one more, and we get first el less or equal to cur
            if (iter == efflist.begin()) return EffectRecord::unknown_effect; // Can't go back one
            --iter;
            Address hit = (*iter).getAddress();
            int sz = (*iter).getSize();
            if (sz == 0 && (hit.getSpace() == addr.getSpace())) // A size of zero indicates the whole space is unaffected
                return EffectRecord.EffectType.unaffected;
            int where = addr.overlap(0, hit, sz);
            if ((where >= 0) && (where + size <= sz))
                return (*iter).getType();
            return EffectRecord::unknown_effect;
        }

        /// \brief Look up a particular EffectRecord from a given list by its Address and size
        ///
        /// The index of the matching EffectRecord from the given list is returned.  Only the first
        /// \e listSize elements are examined, which much be sorted by Address.
        /// If no matching range exists, a negative number is returned.
        ///   - -1 if the Address and size don't overlap any other EffectRecord
        ///   - -2 if there is overlap with another EffectRecord
        ///
        /// \param efflist is the given list
        /// \param listSize is the number of records in the list to search through
        /// \param addr is the starting Address of the record to find
        /// \param size is the size of the record to find
        /// \return the index of the matching record or a negative number
        public static int lookupRecord(List<EffectRecord> efflist, int listSize, Address addr,
            int size)
        {
            if (listSize == 0) return -1;
            EffectRecord cur = new EffectRecord(addr, size);

            List<EffectRecord>::const_iterator begiter = efflist.begin();
            List<EffectRecord>::const_iterator enditer = begiter + listSize;
            
            IEnumerator<EffectRecord> iter = upper_bound(begiter, enditer, cur, EffectRecord.compareByAddress);
            // First element greater than cur  (address must be greater)
            // go back one more, and we get first el less or equal to cur
            if (iter == efflist.begin()) {
                return (iter.Current.getAddress().overlap(0, addr, size) < 0) ? -1 : -2;
            }
            --iter;
            Address closeAddr = iter.Current.getAddress();
            int sz = iter.Current.getSize();
            if (addr == closeAddr && size == sz)
                return iter - begiter;
            return (addr.overlap(0, closeAddr, sz) < 0) ? -1 : -2;
        }
    }
}
