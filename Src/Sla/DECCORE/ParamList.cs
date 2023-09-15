using Sla.CORE;

namespace Sla.DECCORE
{
    /// A group of ParamEntry objects that form a complete set for passing
    /// parameters in one direction (either input or output). The main tasks this class must
    /// perform are:
    ///   - possibleParam() Quick test if a Varnode could ever be a parameter with this prototype
    ///   - fillinMap()   Select trials completing prototype, given analysis info
    ///   - assignMap()   Derive slot.address map, given a list of types
    ///   - checkJoin()   Can two parameters be considered/converted into a single logical parameter
    internal abstract class ParamList
    {
        public enum Model
        {
            p_standard,     ///< Standard input parameter model
            p_standard_out, ///< Standard output (return value) model
            p_register,     ///< Unordered parameter passing locations model
            p_register_out, ///< Multiple possible return value locations model
            p_merged        ///< A merged model (multiple models merged together)
        }

        /// Destructor
        ~ParamList()
        {
        }

        /// Get the type of parameter list
        public abstract Model getType();

        /// \brief Given list of data-types, map the list positions to storage locations
        ///
        /// If we know the function prototype, recover how parameters are actually stored using the model.
        /// \param proto is the ordered list of data-types
        /// \param typefactory is the TypeFactory (for constructing pointers)
        /// \param res will contain the storage locations corresponding to the datatypes

        public abstract void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res);

        /// \brief Given an unordered list of storage locations, calculate a function prototype
        /// A list of input (or output) trials is given, which may have holes, invalid inputs etc.  Decide
        /// on the formal ordered parameter list. Trials within the ParamActive are added, removed, or
        /// reordered as needed.
        /// \param active is the given list of trials
        public abstract void fillinMap(ParamActive active);

        /// \brief Check if the given two storage locations can represent a single logical parameter
        /// Within the conventions of this model, do the two (hi/lo) locations represent
        /// consecutive parameter locations that can be replaced by a single logical parameter.
        /// \param hiaddr is the address of the most significant part of the value
        /// \param hisize is the size of the most significant part in bytes
        /// \param loaddr is the address of the least significant part of the value
        /// \param losize is the size of the least significant part in bytes
        /// \return \b true if the two pieces can be joined
        public abstract bool checkJoin(Address hiaddr, int hisize, Address loaddr, int losize);

        /// \brief Check if it makes sense to split a single storage location into two parameters
        /// A storage location and split point is provided, implying two new storage locations. Does
        /// \b this model allow these locations to be considered parameters.
        /// \param loc is the starting address of provided storage location
        /// \param size is the size of the location in bytes
        /// \param splitpoint is the number of bytes to consider in the first (in address order) piece
        /// \return \b true if the storage location can be split
        public abstract bool checkSplit(Address loc,int size, int splitpoint);

        /// \brief Characterize whether the given range overlaps parameter storage
        /// Does the range naturally fit inside a potential parameter entry from this list or does
        /// it contain a parameter entry. Return one of four enumerations indicating this characterization:
        ///   - no_containment - there is no containment between the range and any parameter in this list
        ///   - contains_unjustified - at least one parameter contains the range
        ///   - contains_justified - at least one parameter contains this range as its least significant bytes
        ///   - contained_by - no parameter contains this range, but the range contains at least one parameter
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the given range
        /// \return the characterization code
        public abstract ParamEntry.Containment characterizeAsParam(Address loc,int size);

        /// \brief Does the given storage location make sense as a parameter
        /// Within \b this model, decide if the storage location can be considered a parameter.
        /// \param loc is the starting address of the storage location
        /// \param size is the number of bytes in the storage location
        /// \return \b true if the location can be a parameter
        public abstract bool possibleParam(Address loc,int size);

        /// \brief Pass-back the slot and slot size for the given storage location as a parameter
        /// This checks if the given storage location acts as a parameter in \b this model and
        /// passes back the number of slots that it occupies.
        /// \param loc is the starting address of the storage location
        /// \param size is the number of bytes in the storage location
        /// \param slot if the \e slot number to pass back
        /// \param slotsize is the number of consumed slots to pass back
        /// \return \b true if the location can be a parameter
        public abstract bool possibleParamWithSlot(Address loc, int size, out int slot, out int slotsize);

        /// \brief Pass-back the biggest parameter contained within the given range
        /// \param loc is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \param res will hold the parameter storage description being passed back
        /// \return \b true if there is at least one parameter contained in the range
        public abstract bool getBiggestContainedParam(Address loc,int size, VarnodeData res);

        /// \brief Check if the given storage location looks like an \e unjustified parameter
        /// The storage for a value may be contained in a normal parameter location but be
        /// unjustified within that container, i.e. the least significant bytes are not being used.
        /// If this is the case, pass back the full parameter location and return \b true.
        /// \param loc is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the full parameter storage to pass back
        /// \return \b true if the given storage is unjustified within its parameter container
        public abstract bool unjustifiedContainer(Address loc,int size, VarnodeData res);

        /// \brief Get the type of extension and containing parameter for the given storage
        /// If the given storage is properly contained within a normal parameter and the model
        /// typically extends a small value into the full container, pass back the full container
        /// and the type of extension.
        /// \param addr is the starting address of the given storage
        /// \param size is the number of bytes in the given storage
        /// \param res is the parameter storage to pass back
        /// \return the extension operator (INT_ZEXT INT_SEXT) or INT_COPY if there is no extension.
        /// INT_PIECE indicates the extension is determined by the specific prototype.
        public abstract OpCode assumedExtension(Address addr, int size, VarnodeData res);

        /// \brief Get the address space associated with any stack based parameters in \b this list.
        /// \return the stack address space, if \b this models parameters passed on the stack, NULL otherwise
        public abstract AddrSpace getSpacebase();

        /// \brief For a given address space, collect all the parameter locations within that space
        /// Pass back the memory ranges for any parameter that is stored in the given address space.
        /// \param spc is the given address space
        /// \param res will hold the set of matching memory ranges
        public abstract void getRangeList(AddrSpace spc, RangeList res);

        /// \brief Return the maximum heritage delay across all possible parameters
        /// Depending on the address space, data-flow for a parameter may not be available until
        /// extra transform passes have completed. This method returns the number of passes
        /// that must occur before we can guarantee that all parameters have data-flow info.
        /// \return the maximum number of passes across all parameters in \b this model
        public abstract int getMaxDelay();

        /// \brief Restore the model from an \<input> or \<output> element in the stream
        /// \param decoder is the stream decoder
        /// \param effectlist is a container collecting EffectRecords across all parameters
        /// \param normalstack is \b true if parameters are pushed on the stack in the normal order
        public abstract void decode(Sla.CORE.Decoder decoder, LinkedList<EffectRecord> effectlist,
            bool normalstack);

        /// Clone this parameter list model
        public abstract ParamList clone();
    }
}
