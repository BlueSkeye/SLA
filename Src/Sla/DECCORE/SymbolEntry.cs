using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    /// \brief A storage location for a particular Symbol
    /// Where a Symbol is stored, as a byte address and a size, is of particular importance
    /// to the decompiler. This class encapsulates this storage meta-data. A single Symbol split
    /// across multiple storage locations is supported by the \b offset and \b size fields. The
    /// \b hash field supports \e dynamic storage, where a Symbol is represented by a constant
    /// or a temporary register. In this case, storage must be tied to the particular p-code
    /// operators using the value.
    /// A particular memory address does \b not have to represent the symbol across all code. Storage
    /// may get recycled for different Symbols at different points in the code. The \b uselimit object
    /// defines the range of instruction addresses over which a particular memory address does
    /// represent a Symbol, with the convention that an empty \b uselimit indicates the storage
    /// holds the Symbol across \e all code.
    internal class SymbolEntry
    {
        //typedef ulong linetype;     ///< The linear element for a rangemap of SymbolEntry
        //typedef EntrySubsort subsorttype;   ///< The sub-sort object for a rangemap
        //typedef EntryInitData inittype; ///< Initialization data for a SymbolEntry in a rangemap

        // friend class Scope;
        /// Symbol object being mapped
        internal Symbol symbol;
        /// Varnode flags specific to this storage location
        private Varnode.varnode_flags extraflags;
        /// Starting address of the storage location
        internal Address addr;
        /// A dynamic storage address (an alternative to \b addr for dynamic symbols)
        internal ulong hash;
        /// Offset into the Symbol that \b this covers
        private int offset;
        /// Number of bytes consumed by \b this (piece of the) storage
        private int size;
        /// Code address ranges where this storage is valid
        internal RangeList uselimit;

        /// Construct a mapping for a Symbol without an address
        /// This SymbolEntry is unintegrated. An address or hash must be provided
        /// either directly or via decode().
        /// \param sym is the Symbol \b this will be a map for
        internal SymbolEntry(Symbol sym)
        {
            symbol = sym;
            extraflags = 0;
            offset = 0;
            hash = 0;
            size = -1;
        }

        internal static IRangemapSubsortTypeInstantiator<EntrySubsort> SubsortInstantiator
            => EntrySubsort.Instanciator.Instance;

        /// Fully initialize \b this
        /// Establish the boundary offsets and fill in additional data
        /// \param data contains the raw initialization data
        /// \param a is the starting offset of the entry
        /// \param b is the ending offset of the entry
        public SymbolEntry(EntryInitData data, ulong a, ulong b)
        {
            addr = new Address(data.space, a);
            size = (int)((b - a) + 1);
            symbol = data.symbol;
            extraflags = data.extraflags;
            offset = data.offset;
            uselimit = data.uselimit;
        }

        /// Construct a dynamic SymbolEntry
        /// This is used specifically for \e dynamic Symbol objects, where the storage location
        /// is attached to a temporary register or a constant. The main address field (\b addr)
        /// is set to \e invalid, and the \b hash becomes the primary location information.
        /// \param sym is the underlying Symbol
        /// \param exfl are the Varnode flags associated with the storage location
        /// \param h is the the hash
        /// \param off if the offset into the Symbol for this (piece of) storage
        /// \param sz is the size in bytes of this (piece of) storage
        /// \param rnglist is the set of code addresses where \b this SymbolEntry represents the Symbol
        public SymbolEntry(Symbol sym, Varnode.varnode_flags exfl, ulong h, int off,
            int sz, RangeList rnglist)
        {
            symbol = sym;
            extraflags = exfl;
            addr = new Address();
            hash = h;
            offset = off;
            size = sz;
            uselimit = rnglist;
        }

        // Is \b this a high or low piece of the whole Symbol
        public bool isPiece()
        {
            return ((extraflags & (Varnode.varnode_flags.precislo | Varnode.varnode_flags.precishi)) != 0);
        }

        /// Is \b storage \e dynamic
        public bool isDynamic()
        {
            return addr.isInvalid();
        }

        /// Is \b this storage \e invalid
        public bool isInvalid() => (addr.isInvalid() && (hash == 0));

        ///< Get all Varnode flags for \b this storage
        public Varnode.varnode_flags getAllFlags() => extraflags | symbol.getFlags();

        /// Get offset of \b this within the Symbol
        public int getOffset() => offset;

        /// Get the first offset of \b this storage location
        public ulong getFirst() => addr.getOffset();

        /// Get the last offset of \b this storage location
        public ulong getLast() => (addr.getOffset() + (uint)size - 1);

        /// Get the sub-sort object
        /// Get data used to sub-sort entries (in a rangemap) at the same address
        /// \return the sub-sort object
        public /*subsorttype*/ EntrySubsort getSubsort()
        {
            // Minimal subsort
            /*subsorttype*/
            EntrySubsort res = new EntrySubsort();
            if ((symbol.getFlags() & Varnode.varnode_flags.addrtied) == 0) {
                Sla.CORE.Range range = uselimit.getFirstRange()
                    ?? throw new LowlevelError("Map entry with empty uselimit");
                res.useindex = range.getSpace().getIndex();
                res.useoffset = range.getFirst();
            }
            return res;
        }

        /// Get the Symbol associated with \b this
        public Symbol getSymbol() => symbol;

        /// Get the starting address of \b this storage
        public Address getAddr() => addr;

        /// Get the hash used to identify \b this storage
        public ulong getHash() => hash;

        /// Get the number of bytes consumed by \b this storage
        public int getSize() => size;

        // Is \b this storage valid for the given code address
        // This storage location may only hold the Symbol value for a limited portion
        // of the code.
        // \param usepoint is the given code address to test
        // \return \b true if \b this storage is valid at the given address
        public bool inUse(Address usepoint)
        {
            if (isAddrTied()) return true; // Valid throughout scope
            if (usepoint.isInvalid()) return false;
            return uselimit.inRange(usepoint, 1);
        }

        /// Get the set of valid code addresses for \b this storage
        public RangeList getUseLimit() => uselimit;

        /// Get the first code address where \b this storage is valid
        public Address getFirstUseAddress()
        {
            Sla.CORE.Range? rng = uselimit.getFirstRange();
            return (rng == null) ? new Address() : rng.getFirstAddr();
        }

        /// Set the range of code addresses where \b this is valid
        public void setUseLimit(RangeList uselim)
        {
            uselimit = uselim;
        }

        ///< Is \b this storage address tied
        public bool isAddrTied()
            => ((symbol.getFlags() & Varnode.varnode_flags.addrtied) != 0);

        /// Update a Varnode data-type from \b this
        /// If the Symbol associated with \b this is type-locked, change the given
        /// Varnode's attached data-type to match the Symbol
        /// \param vn is the Varnode to modify
        /// \return true if the data-type was changed
        public bool updateType(Varnode vn)
        {
            if ((symbol.getFlags() & Varnode.varnode_flags.typelock) != 0) {
                // Type will just get replaced if not locked
                Datatype? dt = getSizedType(vn.getAddr(), vn.getSize());
                if (dt != (Datatype)null)
                    return vn.updateType(dt, true, true);
            }
            return false;
        }

        /// Get the data-type associated with (a piece of) \b this
        /// Return the data-type that matches the given size and address within \b this storage.
        /// NULL is returned if there is no valid sub-type matching the size.
        /// \param inaddr is the given address
        /// \param sz is the given size (in bytes)
        /// \return the matching data-type or NULL
        public Datatype getSizedType(Address inaddr, int sz)
        {
            int off = isDynamic()
                ? offset
                : (int)(inaddr.getOffset() - addr.getOffset()) + offset;
            Datatype cur = symbol.getType() ?? throw new ApplicationException();
            return symbol.getScope().getArch().types.getExactPiece(cur, off, sz);
        }

        // Dump a description of \b this to a stream
        // Give a contained one-line description of \b this storage, suitable for
        // a debug console
        // \param s is the output stream
        public void printEntry(TextWriter s)
        {
            s.Write($"{symbol.getName()} : ");
            if (addr.isInvalid())
                s.Write("<dynamic>");
            else {
                s.Write(addr.getShortcut());
                addr.printRaw(s);
            }
            s.Write($":{(uint)symbol.getType().getSize()} ");
            symbol.getType().printRaw(s);
            s.Write(" : ");
            uselimit.printBounds(s);
        }

        /// Encode \b this to a stream
        /// This writes elements internal to the \<mapsym> element associated with the Symbol.
        /// It encodes the address element (or the \<hash> element for dynamic symbols) and
        /// a \<rangelist> element associated with the \b uselimit.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            if (isPiece()) {
                // Don't save a piece
                return;
            }
            if (addr.isInvalid()) {
                encoder.openElement(ElementId.ELEM_HASH);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_VAL, hash);
                encoder.closeElement(ElementId.ELEM_HASH);
            }
            else {
                addr.encode(encoder);
            }
            uselimit.encode(encoder);
        }

        /// Decode \b this from a stream
        /// Parse either an \<addr> element for storage information or a \<hash> element
        /// if the symbol is dynamic. Then parse the \b uselimit describing the valid
        /// range of code addresses.
        /// \param decoder is the stream decoder
        /// \return the advanced iterator
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.peekElement();
            if (elemId == ElementId.ELEM_HASH) {
                decoder.openElement();
                hash = decoder.readUnsignedInteger(AttributeId.ATTRIB_VAL);
                addr = new Address();
                decoder.closeElement(elemId);
            }
            else {
                addr = Address.decode(decoder);
                hash = 0;
            }
            uselimit.decode(decoder);
        }

        /// \brief Initialization data for a SymbolEntry to facilitate a rangemap
        /// This is all the raw pieces of a SymbolEntry for a (non-dynamic) Symbol except
        /// the offset of the main address and the size, which are provided by the
        /// (rangemap compatible) SymbolEntry constructor.
        public class EntryInitData
        {
            // friend class SymbolEntry;
            // The address space of the main SymbolEntry starting address
            internal AddrSpace space;
            // The symbol being mapped
            internal Symbol symbol;
            // Varnode flags specific to the storage location
            internal Varnode.varnode_flags extraflags;
            // Starting offset of the portion of the Symbol being covered
            internal int offset;
            // Reference to the range of code addresses for which the storage is valid
            internal readonly RangeList uselimit = new RangeList();

            public EntryInitData(Symbol sym, Varnode.varnode_flags exfl, AddrSpace spc,
                int off, RangeList ul)
            {
                uselimit = ul;
                symbol = sym;
                extraflags = exfl;
                space = spc;
                offset = off;
            }
        }

        /// \brief Class for sub-sorting different SymbolEntry objects at the same address
        /// This is built from the SymbolEntry \b uselimit object (see SymbolEntry::getSubsort())
        /// Relevant portions of an Address object or pulled out for smaller storage and quick comparisons.
        public class EntrySubsort : IComparable<EntrySubsort>
        {
            // friend class SymbolEntry;
            // Index of the sub-sorting address space
            internal int useindex;
            // Offset into the sub-sorting address space
            internal ulong useoffset;

            // Construct given a sub-sorting address
            public EntrySubsort(Address addr) {
                useindex = addr.getSpace().getIndex();
                useoffset = addr.getOffset();
            }

            // Construct earliest possible sub-sort
            public EntrySubsort()
            {
                useindex = 0;
                useoffset = 0;
            }

            ///// \brief Given a boolean value, construct the earliest/latest possible sub-sort
            ///// \param val is \b true for the latest and \b false for the earliest possible sub-sort
            //public EntrySubsort(bool val)
            //{
            //    if (val) { useindex = 0xffff; } // Greater than any real values
            //    else { useindex = 0; useoffset = 0; }   // Less than any real values
            //}

            ///// \brief Copy constructor
            //public EntrySubsort(EntrySubsort op2) {
            //    useindex = op2.useindex;
            //    useoffset = op2.useoffset;
            //}

            ///// \brief Compare \b this with another sub-sort
            //public static bool operator <(EntrySubsort op1, EntrySubsort op2)
            //{
            //    if (op1.useindex != op2.useindex)
            //        return (op1.useindex < op2.useindex);
            //    return (op1.useoffset < op2.useoffset);
            //}

            public int CompareTo(EntrySubsort? x)
            {
                if (null == x) throw new ApplicationException();
                int result = this.useindex.CompareTo(x.useindex);
                if (0 == result) {
                    result = this.useoffset.CompareTo(x.useoffset);
                }
                return result;
            }

            internal class Instanciator : IRangemapSubsortTypeInstantiator<EntrySubsort>
            {
                internal static Instanciator Instance = new Instanciator();

                private Instanciator()
                {
                }

                /// \brief Given a boolean value, construct the earliest/latest possible sub-sort
                /// \param val is \b true for the latest and \b false for the earliest possible sub-sort
                public EntrySubsort Create(bool value)
                {
                    EntrySubsort result = new EntrySubsort();
                    if (value) {
                        // Greater than any real values
                        result.useindex = 0xffff;
                    }
                    else {
                        // Less than any real values
                        result.useindex = 0;
                        result.useoffset = 0;
                    }
                    return result;
                }

                public EntrySubsort Create(EntrySubsort cloned)
                {
                    EntrySubsort result = new EntrySubsort() {
                        useindex = cloned.useindex,
                        useoffset = cloned.useoffset
                    };
                    return result;
                }
            }
        }
    }
}
