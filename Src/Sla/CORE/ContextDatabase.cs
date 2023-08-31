using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using TrackedSet = System.Collections.Generic.List<Sla.CORE.TrackedContext>;

namespace Sla.CORE
{
    /// \brief An interface to a database of disassembly/decompiler \b context information
    /// \b Context \b information is a set of named variables that hold concrete values at specific
    /// addresses in the target executable being analyzed. A variable can hold different values at
    /// different addresses, but a specific value at a specific address never changes. Analysis recovers
    /// these values over time, populating this database, and querying this database lets analysis
    /// provides concrete values for memory locations in context.
    /// Context variables come in two flavors:
    ///  - \b Low-level \b context \b variables:
    ///      These can affect instruction decoding. These can be as small as a single bit and need to
    ///      be defined in the Sleigh specification (so that Sleigh knows how they effect disassembly).
    ///      These variables are not mapped to normal memory locations with an address space and offset
    ///      (although they often have a corresponding embedding into a normal memory location).
    ///      The model to keep in mind is a control register with specialized bit-fields within it.
    ///  - \b High-level \b tracked \b variables:
    ///      These are normal memory locations that are to be treated as constants across some range of
    ///      code. These are normally registers that are being tracked by the compiler outside the
    ///      domain of normal local and global variables. They have a specific value established by
    ///      the compiler coming into a function but are not supposed to be interpreted as a high-level
    ///      variable. Typical examples are the direction flag (for \e string instructions) and segment
    ///      registers. All tracked variables are interpreted as a constant value at the start of a
    ///      function, although the memory location can be recycled for other calculations later in the
    ///      function.
    /// Low-level context variables can be queried and set by name -- getVariable(), setVariable(),
    /// setVariableRegion() -- but the disassembler accesses all the variables at an address as a group
    /// via getContext(), setContextChangePoint(), setContextRegion().  In this setting, all the values
    /// are packed together in an array of words, a context \e blob (See ContextBitRange).
    /// Tracked variables are also queried as a group via getTrackedSet() and createSet().  These return
    /// a list of TrackedContext objects.
    public abstract class ContextDatabase
    {
        /// \brief Encode all tracked register values for a specific address to a stream
        /// Encode all the tracked register values associated with a specific target address
        /// as a \<tracked_pointset> tag.
        /// \param encoder is the stream encoder
        /// \param addr is the specific address we have tracked values for
        /// \param vec is the list of tracked values
        internal static void encodeTracked(Encoder encoder, Address addr, TrackedSet vec)
        {
            if (0 == vec.Count) {
                return;
            }
            encoder.openElement(ElementId.ELEM_TRACKED_POINTSET);
            addr.getSpace().encodeAttributes(encoder, addr.getOffset());
            for (int i = 0; i < vec.Count; ++i) {
                vec[i].encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_TRACKED_POINTSET);
        }

        /// \brief Restore a sequence of tracked register values from the given stream decoder
        /// Parse a \<tracked_pointset> element, decoding each child in turn to populate a list of
        /// TrackedContext objects.
        /// \param decoder is the given stream decoder
        /// \param vec is the container that will hold the new TrackedContext objects
        internal static void decodeTracked(Decoder decoder, TrackedSet vec)
        {
            // Clear out any old stuff
            vec.Clear();
            while (decoder.peekElement() != 0) {
                TrackedContext newContext = new TrackedContext();
                newContext.decode(decoder);
                vec.Add(newContext);
            }
        }

        /// \brief Retrieve the context variable description object by name
        /// If the variable doesn't exist an exception is thrown.
        /// \param nm is the name of the context value
        /// \return the ContextBitRange object matching the name
        protected abstract ContextBitRange getVariable(string nm);

        /// \brief Grab the context blob(s) for the given address range, marking bits that will be set
        /// This is an internal routine for obtaining the actual memory regions holding context values
        /// for the address range.  This also informs the system which bits are getting set. A split is forced
        /// at the first address, and at least one memory region is passed back. The second address can be
        /// invalid in which case the memory region passed back is valid from the first address to whatever
        /// the next split point is.
        /// \param res will hold pointers to memory regions for the given range
        /// \param addr1 is the starting address of the range
        /// \param addr2 is (1 past) the last address of the range or is invalid
        /// \param num is the word index for the context value that will be set
        /// \param mask is a mask of the value being set (within its word)
        protected abstract void getRegionForSet(List<uint[]> res, Address addr1,
            Address addr2, int num, uint mask);

        /// \brief Grab the context blob(s) starting at the given address up to the first point of change
        /// This is an internal routine for obtaining the actual memory regions holding context values
        /// starting at the given address.  A specific context value is specified, and all memory regions
        /// are returned up to the first address where that particular context value changes.
        /// \param res will hold pointers to memory regions being passed back
        /// \param addr is the starting address of the regions to fetch
        /// \param num is the word index for the specific context value being set
        /// \param mask is a mask of the context value being set (within its word)
        protected abstract void getRegionToChangePoint(List<uint[]> res, Address addr,
            int num, uint mask);

        /// \brief Retrieve the memory region holding all default context values
        /// This fetches the active memory holding the default context values on top of which all other context
        /// values are overlaid.
        /// \return the memory region holding all the default context values
        protected abstract uint[] getDefaultValue();

        /////< Destructor
        //public ~ContextDatabase()
        //{
        //}

        /// \brief Retrieve the number of words (uint) in a context \e blob
        /// \return the number of words
        public abstract int getContextSize();

        /// \brief Register a new named context variable (as a bit range) with the database
        /// A new variable is registered by providing a name and the range of bits the value will occupy
        /// within the context blob.  The full blob size is automatically increased if necessary.  The variable
        /// must be contained within a single word, and all variables must be registered before any values can
        /// be set.
        /// \param nm is the name of the new variable
        /// \param sbit is the position of the variable's most significant bit within the blob
        /// \param ebit is the position of the variable's least significant bit within the blob
        internal abstract void registerVariable(string nm, int sbit, int ebit);

        /// \brief Get the context blob of values associated with a given address
        /// \param addr is the given address
        /// \return the memory region holding the context values for the address
        internal abstract uint[] getContext(Address addr);

        /// \brief Get the context blob of values associated with a given address and its bounding offsets
        /// In addition to the memory region, the range of addresses for which the region is valid
        /// is passed back as offsets into the address space.
        /// \param addr is the given address
        /// \param first will hold the starting offset of the valid range
        /// \param last will hold the ending offset of the valid range
        /// \return the memory region holding the context values for the address
        internal abstract uint[] getContext(Address addr, out ulong first, out ulong last);

        /// \brief Get the set of default values for all tracked registers
        /// \return the list of TrackedContext objects
        internal abstract ref TrackedSet getTrackedDefault();

        /// \brief Get the set of tracked register values associated with the given address
        /// \param addr is the given address
        /// \return the list of TrackedContext objects
        internal abstract TrackedSet getTrackedSet(Address addr);

        /// \brief Create a tracked register set that is valid over the given range
        /// This really should be an internal routine.  The created set is empty, old values are blown
        /// away.  If old/default values are to be preserved, they must be copied back @in.
        /// \param addr1 is the starting address of the given range
        /// \param addr2 is (1 past) the ending address of the given range
        /// \return the empty set of tracked register values
        internal abstract TrackedSet createSet(Address addr1, Address addr2);

        /// \brief Encode the entire database to a stream
        /// \param encoder is the stream encoder
        internal abstract void encode(Sla.CORE.Encoder encoder);

        /// \brief Restore the state of \b this database object from the given stream decoder
        /// \param decoder is the given stream decoder
        internal abstract void decode(Sla.CORE.Decoder decoder);

        /// \brief Add initial context state from elements in the compiler/processor specifications
        /// Parse a \<context_data> element from the given stream decoder from either the compiler
        /// or processor specification file for the architecture, initializing this database.
        /// \param decoder is the given stream decoder
        internal abstract void decodeFromSpec(Sla.CORE.Decoder decoder);

        ///< Provide a default value for a context variable
        /// The default value is returned for addresses that have not been overlaid with other values.
        /// \param nm is the name of the context variable
        /// \param val is the default value to establish
        internal void setVariableDefault(string nm, uint val)
        {
            getVariable(nm).setValue(getDefaultValue(), val);
        }

        ///< Retrieve the default value for a context variable
        /// This will return the default value used for addresses that have not been overlaid with other values.
        /// \param nm is the name of the context variable
        /// \return the variable's default value
        protected uint getDefaultValue(ref string nm)
        {
            return getVariable(nm).getValue(getDefaultValue());
        }

        ///< Set a context value at the given address
        /// The variable will be changed to the new value, starting at the given address up to the next
        /// point of change.
        /// \param nm is the name of the context variable
        /// \param addr is the given address
        /// \param value is the new value to set
        protected void setVariable(ref string nm, ref Address addr, uint value)
        {
            ContextBitRange bitrange = getVariable(nm);
            int num = bitrange.getWord();
            uint mask = bitrange.getMask() << bitrange.getShift();

            List<uint[]> contvec = new List<uint[]>();
            getRegionToChangePoint(contvec, addr, num, mask);
            for (int i = 0; i < contvec.Count; ++i) {
                bitrange.setValue(contvec[i], value);
            }
        }

        /// Retrieve a context value at the given address
        /// If a value has not been explicit set for an address range containing the given
        /// address, the default value for the variable is returned
        /// \param nm is the name of the context variable
        /// \param addr is the address for which the specific value is needed
        /// \return the context variable value for the address
        protected uint getVariable(ref string nm, ref Address addr)
        {
            ContextBitRange bitrange = getVariable(nm);
            uint[] context = getContext(addr);
            return bitrange.getValue(context);
        }

        /// \brief Set a specific context value starting at the given address
        /// The new value is \e painted across an address range starting, starting with the given address
        /// up to the point where another change for the variable was specified. No other context variable
        /// is changed, inside (or outside) the range.
        /// \param addr is the given starting address
        /// \param num is the index of the word (within the context blob) of the context variable
        /// \param mask is the mask delimiting the context variable (within its word)
        /// \param value is the (already shifted) value being set
        internal void setContextChangePoint(Address addr, int num, uint mask, uint value)
        {
            List<uint[]> contvec = new List<uint[]>();
            getRegionToChangePoint(contvec, addr, num, mask);
            for (int i = 0; i < contvec.Count; ++i) {
                uint[] newcontext = contvec[i];
                uint val = newcontext[num];
                // Clear range to zero
                val &= ~mask;
                val |= value;
                newcontext[num] = val;
            }
        }

        /// \brief Set a context variable value over a given range of addresses
        /// The new value is \e painted over an explicit range of addresses. No other context variable is
        /// changed inside (or outside) the range.
        /// \param addr1 is the starting address of the given range
        /// \param addr2 is the ending address of the given range
        /// \param num is the index of the word (within the context blob) of the context variable
        /// \param mask is the mask delimiting the context variable (within its word)
        /// \param value is the (already shifted) value being set
        internal void setContextRegion(Address addr1, Address addr2, int num, uint mask,
            uint value)
        {
            List<uint[]> vec = new List<uint[]>();
            getRegionForSet(vec, addr1, addr2, num, mask);
            for (int i = 0; i < vec.Count; ++i) {
                vec[i][num] = (vec[i][num] & ~mask) | value;
            }
        }

        /// \brief Set a context variable by name over a given range of addresses
        /// The new value is \e painted over an explicit range of addresses. No other context variable is
        /// changed inside (or outside) the range.
        /// \param nm is the name of the context variable to set
        /// \param begad is the starting address of the given range
        /// \param endad is the ending address of the given range
        /// \param value is the new value to set
        protected void setVariableRegion(ref string nm, ref Address begad, ref Address endad,
            uint value)
        {
            ContextBitRange bitrange = getVariable(nm);
            List<uint[]> vec = new List<uint[]>();
            getRegionForSet(vec, begad, endad, bitrange.getWord(),
                bitrange.getMask() << bitrange.getShift());
            for (int i = 0; i < vec.Count; ++i) {
                bitrange.setValue(vec[i], value);
            }
        }

        /// \brief Get the value of a tracked register at a specific address
        /// A specific storage region and code address is given.  If the region is tracked the value at
        /// the address is retrieved.  If the specified storage region is contained in the tracked region,
        /// the retrieved value is trimmed to match the containment before returning it. If the region is not
        /// tracked, a value of 0 is returned.
        /// \param mem is the specified storage region
        /// \param point is the code address
        /// \return the tracked value or zero
        internal ulong getTrackedValue(VarnodeData mem, Address point)
        {
            TrackedSet tset = getTrackedSet(point);
            ulong endoff = mem.offset + mem.size - 1;
            ulong tendoff;
            for (int i = 0; i < tset.Count; ++i) {
                TrackedContext tcont = tset[i];
                // tcont must contain -mem-
                if (tcont.loc.space != mem.space) {
                    continue;
                }
                if (tcont.loc.offset > mem.offset) {
                    continue;
                }
                tendoff = tcont.loc.offset + tcont.loc.size - 1;
                if (tendoff < endoff) {
                    continue;
                }
                ulong res = tcont.val;
                // If we have proper containment, trim value based on endianness
                if (tcont.loc.space.isBigEndian()) {
                    if (endoff != tendoff) {
                        res >>= (int)(8 * (tendoff - mem.offset));
                    }
                }
                else {
                    if (mem.offset != tcont.loc.offset) {
                        res >>= (int)(8 * (mem.offset - tcont.loc.offset));
                    }
                }
                // Final trim based on size
                res &= Globals.calc_mask(mem.size);
                return res;
            }
            return 0;
        }
    }
}
