using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Sla.DECCORE
{
    /// \brief The base class for a symbol in a symbol table or scope
    /// At its most basic, a Symbol is a \b name and a \b data-type.
    /// Practically a Symbol knows what Scope its in, how it should be
    /// displayed, and the symbols \e category. A category is a subset
    /// of symbols that are stored together for quick access.
    internal class Symbol
    {
        //friend class Scope;
        //friend class ScopeInternal;
        //friend class SymbolCompareName;
        /// Base of internal ID's
        public const ulong ID_BASE = 0x4000000000000000UL;

        /// The scope that owns this symbol
        protected Scope scope;
        /// The local name of the symbol
        protected string name;
        /// Name to use when displaying symbol in output
        protected string displayName;
        /// The symbol's data-type
        protected Datatype type;
        /// id to distinguish symbols with the same name
        protected uint nameDedup;
        /// Varnode-like properties of the symbol
        protected uint flags;
        // only typelock,namelock,readonly,externref
        // addrtied, persist inherited from scope
        /// Flags affecting the display of this symbol
        protected uint dispflags;
        /// Special category (\b function_parameter, \b equate, etc.)
        protected short category;
        /// Index within category
        protected ushort catindex;
        /// Unique id, 0=unassigned
        protected ulong symbolId;
        /// List of storage locations labeled with \b this Symbol
        protected List<IEnumerator<SymbolEntry>> mapentry;
        /// Scope associated with current depth resolution
        protected /*mutable const*/ Scope? depthScope;
        /// Number of namespace elements required to resolve symbol in current scope
        protected /*mutable*/ int depthResolution;
        /// Number of SymbolEntries that map to the whole Symbol
        protected uint wholeCount;
        
        ~Symbol()
        {
        }

        ///< Set the display format for \b this Symbol
        /// Force a specific display format for constant symbols
        /// \param val is the format:  force_hex, force_dec, force_oct, etc.
        protected void setDisplayFormat(uint val)
        {
            dispflags &= 0xfffffff8;
            dispflags |= val;
        }

        /// Calculate if \b size_typelock property is on
        /// Examine the data-type to decide if the Symbol has the special property
        /// called \b size_typelock, which indicates the \e size of the Symbol
        /// is locked, but the data-type is not locked (and can float)
        protected void checkSizeTypeLock()
        {
            dispflags &= ~((uint4)size_typelock);
            if (isTypeLocked() && (type->getMetatype() == TYPE_UNKNOWN))
                dispflags |= size_typelock;
        }

        /// Toggle whether \b this is the "this" pointer for a class method
        /// \param val is \b true if we are the "this" pointer
        protected void setThisPointer(bool val)
        {
            if (val)
                dispflags |= is_this_ptr;
            else
                dispflags &= ~((uint4)is_this_ptr);
        }

        /// \brief Possible display (dispflag) properties for a Symbol
        public enum DisplayFlags
        {
            force_hex = 1,      ///< Force hexadecimal printing of constant symbol
            force_dec = 2,      ///< Force decimal printing of constant symbol
            force_oct = 3,      ///< Force octal printing of constant symbol
            force_bin = 4,      ///< Force binary printing of constant symbol
            force_char = 5,     ///< Force integer to be printed as a character constant
            size_typelock = 8,          ///< Only the size of the symbol is typelocked
            isolate = 16,       ///< Symbol should not speculatively merge automatically
            merge_problems = 32,    ///< Set if some SymbolEntrys did not get merged
            is_this_ptr = 64        ///< We are the "this" symbol for a class method
        }

        /// \brief The possible specialize Symbol \e categories
        public enum SymbolCategory
        {
            no_category = -1,       ///< Symbol is not in a special category
            function_parameter = 0, ///< The Symbol is a parameter to a function
            equate = 1,         ///< The Symbol holds \e equate information about a constant
            union_facet = 2     ///< Symbol holding read or write facing union field information
        }

        /// Construct given a name and data-type
        /// \param sc is the scope containing the new symbol
        /// \param nm is the local name of the symbol
        /// \param ct is the data-type of the symbol
        public Symbol(Scope sc, string nm, Datatype ct)
        {
            scope = sc;
            name = nm;
            displayName = nm;
            nameDedup = 0;
            type = ct;
            flags = 0;
            dispflags = 0;
            category = no_category;
            catindex = 0;
            symbolId = 0;
            wholeCount = 0;
            depthScope = null;
            depthResolution = 0;
        }

        /// Construct for use with decode()
        /// \param sc is the scope containing the new symbol
        public Symbol(Scope sc)
        {
            scope = sc;
            nameDedup = 0;
            type = (Datatype*)0;
            flags = 0;
            dispflags = 0;
            category = no_category;
            catindex = 0;
            symbolId = 0;
            wholeCount = 0;
            depthScope = (Scope*)0;
            depthResolution = 0;
        }

        /// Get the local name of the symbol
        public string getName() => name;

        /// Get the name to display in output
        public string getDisplayName() => displayName;

        /// Get the data-type
        public Datatype getType() => type;

        /// Get a unique id for the symbol
        public ulong getId() => symbolId;

        /// Get the boolean properties of the Symbol
        public uint getFlags() => flags;

        /// Get the format to display the Symbol in
        public uint getDisplayFormat() => (dispflags & 7);

        /// Get the Symbol category
        public short getCategory() => category;

        /// Get the position of the Symbol within its category
        public ushort getCategoryIndex() => catindex;

        /// Is the Symbol type-locked
        public bool isTypeLocked() => ((flags&Varnode::typelock)!= 0);

        /// Is the Symbol name-locked
        public bool isNameLocked() => ((flags&Varnode::namelock)!= 0);

        /// Is the Symbol size type-locked
        public bool isSizeTypeLocked() => ((dispflags & size_typelock)!= 0);

        /// Is the Symbol volatile
        public bool isVolatile() => ((flags & Varnode::volatil)!= 0);

        /// Is \b this the "this" pointer
        public bool isThisPointer() => ((dispflags & is_this_ptr)!= 0);

        /// Is storage really a pointer to the true Symbol
        public bool isIndirectStorage() => ((flags&Varnode::indirectstorage)!= 0);

        /// Is this a reference to the function return value
        public bool isHiddenReturn() => ((flags&Varnode::hiddenretparm)!= 0);

        /// Does \b this have an undefined name
        public bool isNameUndefined()
        {
            return ((name.size() == 15) && (0 == name.compare(0, 7, "$$undef")));
        }

        /// Does \b this have more than one \e entire mapping
        public bool isMultiEntry() => (wholeCount > 1);

        /// Were some SymbolEntrys not merged
        public bool hasMergeProblems() => ((dispflags & merge_problems)!= 0);

        /// Mark that some SymbolEntrys could not be merged
        public void setMergeProblems()
        {
            dispflags |= merge_problems;
        }

        public bool isIsolated() => ((dispflags & isolate)!= 0); ///< Return \b true if \b this is isolated from speculative merging

        /// Set whether \b this Symbol should be speculatively merged
        /// If the given value is \b true, any Varnodes that map directly to \b this Symbol,
        /// will not be speculatively merged with other Varnodes.  (Required merges will still happen).
        /// \param val is the given boolean value
        public void setIsolated(bool val)
        {
            if (val)
            {
                dispflags |= isolate;
                flags |= Varnode::typelock;     // Isolated Symbol must be typelocked
                checkSizeTypeLock();
            }
            else
                dispflags &= ~((uint4)isolate);
        }

        /// Get the scope owning \b this Symbol
        public Scope getScope() => scope;

        /// Get the first entire mapping of the symbol
        /// \return the first SymbolEntry
        public SymbolEntry getFirstWholeMap()
        {
            if (mapentry.empty())
                throw new LowlevelError("No mapping for symbol: " + name);
            return &(*mapentry[0]);
        }

        /// Get first mapping of the symbol that contains the given Address
        /// This method may return a \e partial entry, where the SymbolEntry is only holding
        /// part of the whole Symbol.
        /// \param addr is an address within the desired storage location of the Symbol
        /// \return the first matching SymbolEntry
        public SymbolEntry? getMapEntry(Address addr)
        {
            SymbolEntry* res;
            for (int4 i = 0; i < mapentry.size(); ++i) {
                res = &(*mapentry[i]);
                Address entryaddr = res->getAddr();
                if (addr.getSpace() != entryaddr.getSpace()) continue;
                if (addr.getOffset() < entryaddr.getOffset()) continue;
                int4 diff = (int4)(addr.getOffset() - entryaddr.getOffset());
                if (diff >= res->getSize()) continue;
                return res;
            }
            //  throw new LowlevelError("No mapping at desired address for symbol: "+name);
            return null;
        }

        /// Return the number of SymbolEntrys
        public int numEntries() => mapentry.Count;

        /// Return the i-th SymbolEntry for \b this Symbol
        public SymbolEntry getMapEntry(int i) => &(*mapentry[i]);

        /// Position of given SymbolEntry within \b this multi-entry Symbol
        /// Among all the SymbolEntrys that map \b this entire Symbol, calculate
        /// the position of the given SymbolEntry within the list.
        /// \param entry is the given SymbolEntry
        /// \return its position within the list or -1 if it is not in the list
        public int getMapEntryPosition(SymbolEntry entry)
        {
            int4 pos = 0;
            for (int4 i = 0; i < mapentry.size(); ++i)
            {
                SymbolEntry tmp = &(*mapentry[i]);
                if (tmp == entry)
                    return pos;
                if (entry->getSize() == type->getSize())
                    pos += 1;
            }
            return -1;
        }

        /// Get number of scope names needed to resolve \b this symbol
        /// For a given context scope where \b this Symbol is used, determine how many elements of
        /// the full namespace path need to be printed to correctly distinguish it.
        /// A value of 0 means the base symbol name is visible and not overridden in the context scope.
        /// A value of 1 means the base name may be overridden, but the parent scope name is not.
        /// The minimal number of names that distinguishes the symbol name uniquely within the
        /// use scope is returned.
        /// \param useScope is the given scope where the symbol is being used
        /// \return the number of (extra) names needed to distinguish the symbol
        public int getResolutionDepth(Scope useScope)
        {
            if (scope == useScope) return 0;    // Symbol is in scope where it is used
            if (useScope == null) {  // Treat null useScope as resolving the full path
                Scope point = scope;
                int4 count = 0;
                while (point != null) {
                    count += 1;
                    point = point->getParent();
                }
                return count - 1;   // Don't print global scope
            }
            if (depthScope == useScope)
                return depthResolution;
            depthScope = useScope;
            Scope distinguishScope = scope->findDistinguishingScope(useScope);
            depthResolution = 0;
            string distinguishName;
            Scope terminatingScope;
            if (distinguishScope == null) {  // Symbol scope is ancestor of use scope
                distinguishName = name;
                terminatingScope = scope;
            }
            else {
                distinguishName = distinguishScope->getName();
                Scope currentScope = scope;
                while (currentScope != distinguishScope)
                {   // For any scope up to the distinguishing scope
                    depthResolution += 1;           // Print its name
                    currentScope = currentScope->getParent();
                }
                depthResolution += 1;       // Also print the distinguishing scope name
                terminatingScope = distinguishScope->getParent();
            }
            if (useScope->isNameUsed(distinguishName, terminatingScope))
                depthResolution += 1;       // Name was overridden, we need one more distinguishing name
            return depthResolution;
        }

        /// Encode basic Symbol properties as attributes
        /// \param encoder is the stream encoder
        public void encodeHeader(Encoder encoder)
        {
            encoder.writeString(ATTRIB_NAME, name);
            encoder.writeUnsignedInteger(ATTRIB_ID, getId());
            if ((flags & Varnode::namelock) != 0)
                encoder.writeBool(ATTRIB_NAMELOCK, true);
            if ((flags & Varnode::typelock) != 0)
                encoder.writeBool(ATTRIB_TYPELOCK, true);
            if ((flags & Varnode::readonly)!= 0)
                encoder.writeBool(ATTRIB_READONLY, true);
            if ((flags & Varnode::volatil) != 0)
                encoder.writeBool(ATTRIB_VOLATILE, true);
            if ((flags & Varnode::indirectstorage) != 0)
                encoder.writeBool(ATTRIB_INDIRECTSTORAGE, true);
            if ((flags & Varnode::hiddenretparm) != 0)
                encoder.writeBool(ATTRIB_HIDDENRETPARM, true);
            if ((dispflags & isolate) != 0)
                encoder.writeBool(ATTRIB_MERGE, false);
            if ((dispflags & is_this_ptr) != 0)
                encoder.writeBool(ATTRIB_THISPTR, true);
            int4 format = getDisplayFormat();
            if (format != 0)
            {
                encoder.writeString(ATTRIB_FORMAT, Datatype::decodeIntegerFormat(format));
            }
            encoder.writeSignedInteger(ATTRIB_CAT, category);
            if (category >= 0)
                encoder.writeUnsignedInteger(ATTRIB_INDEX, catindex);
        }

        /// Decode basic Symbol properties from a \<symbol> element
        /// \param decoder is the stream decoder
        public void decodeHeader(Decoder decoder)
        {
            name.clear();
            displayName.clear();
            category = no_category;
            symbolId = 0;
            for (; ; )
            {
                uintb attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_CAT)
                {
                    category = decoder.readSignedInteger();
                }
                else if (attribId == ATTRIB_FORMAT)
                {
                    dispflags |= Datatype::encodeIntegerFormat(decoder.readString());
                }
                else if (attribId == ATTRIB_HIDDENRETPARM)
                {
                    if (decoder.readBool())
                        flags |= Varnode::hiddenretparm;
                }
                else if (attribId == ATTRIB_ID)
                {
                    symbolId = decoder.readUnsignedInteger();
                    if ((symbolId >> 56) == (ID_BASE >> 56))
                        symbolId = 0;       // Don't keep old internal id's
                }
                else if (attribId == ATTRIB_INDIRECTSTORAGE)
                {
                    if (decoder.readBool())
                        flags |= Varnode::indirectstorage;
                }
                else if (attribId == ATTRIB_MERGE)
                {
                    if (!decoder.readBool())
                    {
                        dispflags |= isolate;
                        flags |= Varnode::typelock;
                    }
                }
                else if (attribId == ATTRIB_NAME)
                    name = decoder.readString();
                else if (attribId == ATTRIB_NAMELOCK)
                {
                    if (decoder.readBool())
                        flags |= Varnode::namelock;
                }
                else if (attribId == ATTRIB_READONLY)
                {
                    if (decoder.readBool())
                        flags |= Varnode::readonly;
                }
                else if (attribId == ATTRIB_TYPELOCK)
                {
                    if (decoder.readBool())
                        flags |= Varnode::typelock;
                }
                else if (attribId == ATTRIB_THISPTR)
                {
                    if (decoder.readBool())
                        dispflags |= is_this_ptr;
                }
                else if (attribId == ATTRIB_VOLATILE)
                {
                    if (decoder.readBool())
                        flags |= Varnode::volatil;
                }
                else if (attribId == ATTRIB_LABEL)
                {
                    displayName = decoder.readString();
                }
            }
            if (category == function_parameter)
            {
                catindex = decoder.readUnsignedInteger(ATTRIB_INDEX);
            }
            else
                catindex = 0;
            if (displayName.size() == 0)
                displayName = name;
        }

        /// Encode details of the Symbol to a stream
        /// Encode the data-type for the Symbol
        /// \param encoder is the stream encoder
        public void encodeBody(Encoder encoder)
        {
            type->encodeRef(encoder);
        }

        /// Decode details of the Symbol from a \<symbol> element
        /// \param decoder is the stream decoder
        public void decodeBody(Decoder decoder)
        {
            type = scope->getArch()->types->decodeType(decoder);
            checkSizeTypeLock();
        }

        /// Encode \b this Symbol to a stream
        /// \param encoder is the stream encoder
        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_SYMBOL);
            encodeHeader(encoder);
            encodeBody(encoder);
            encoder.closeElement(ELEM_SYMBOL);
        }

        /// Decode \b this Symbol from a stream
        /// Parse a Symbol from the next element in the stream
        /// \param decoder is the stream decoder
        public override void decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_SYMBOL);
            decodeHeader(decoder);

            decodeBody(decoder);
            decoder.closeElement(elemId);
        }

        ///< Get number of bytes consumed within the address->symbol map
        /// Get the number of bytes consumed by a SymbolEntry representing \b this Symbol.
        /// By default, this is the number of bytes consumed by the Symbol's data-type.
        /// This gives the amount of leeway a search has when the address queried does not match
        /// the exact address of the Symbol. With functions, the bytes consumed by a SymbolEntry
        /// may not match the data-type size.
        /// \return the number of bytes in a default SymbolEntry
        public override int getBytesConsumed()
        {
            return type->getSize();
        }
    }
}
