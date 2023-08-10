using Sla.CORE;
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

        /// \brief Possible display (dispflag) properties for a Symbol
        [Flags()]
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
            is_this_ptr = 64,        ///< We are the "this" symbol for a class method
            MASK = 7
        }

        /// \brief The possible specialize Symbol \e categories
        public enum SymbolCategory
        {
            no_category = -1,       ///< Symbol is not in a special category
            function_parameter = 0, ///< The Symbol is a parameter to a function
            equate = 1,         ///< The Symbol holds \e equate information about a constant
            union_facet = 2     ///< Symbol holding read or write facing union field information
        }

        /// Base of internal ID's
        public const ulong ID_BASE = 0x4000000000000000UL;

        /// The scope that owns this symbol
        protected Scope scope;
        /// The local name of the symbol
        internal string name;
        /// Name to use when displaying symbol in output
        protected string displayName;
        /// The symbol's data-type
        internal Datatype? type;
        /// id to distinguish symbols with the same name
        internal uint nameDedup;
        /// Varnode-like properties of the symbol
        internal Varnode.varnode_flags flags;
        // only typelock,namelock,readonly,externref
        // addrtied, persist inherited from scope
        /// Flags affecting the display of this symbol
        protected DisplayFlags dispflags;
        /// Special category (\b function_parameter, \b equate, etc.)
        internal SymbolCategory category;
        /// Index within category
        internal ushort catindex;
        /// Unique id, 0=unassigned
        internal ulong symbolId;
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
        protected void setDisplayFormat(DisplayFlags val)
        {
            dispflags &= (DisplayFlags)0xfffffff8;
            dispflags |= val;
        }

        /// Calculate if \b size_typelock property is on
        /// Examine the data-type to decide if the Symbol has the special property
        /// called \b size_typelock, which indicates the \e size of the Symbol
        /// is locked, but the data-type is not locked (and can float)
        protected void checkSizeTypeLock()
        {
            dispflags &= ~(DisplayFlags.size_typelock);
            if (isTypeLocked() && (type.getMetatype() == type_metatype.TYPE_UNKNOWN))
                dispflags |= DisplayFlags.size_typelock;
        }

        /// Toggle whether \b this is the "this" pointer for a class method
        /// \param val is \b true if we are the "this" pointer
        internal void setThisPointer(bool val)
        {
            if (val)
                dispflags |= DisplayFlags.is_this_ptr;
            else
                dispflags &= ~(DisplayFlags.is_this_ptr);
        }

        /// Construct given a name and data-type
        /// \param sc is the scope containing the new symbol
        /// \param nm is the local name of the symbol
        /// \param ct is the data-type of the symbol
        public Symbol(Scope sc, string nm, Datatype? ct)
        {
            scope = sc;
            name = nm;
            displayName = nm;
            nameDedup = 0;
            type = ct;
            flags = 0;
            dispflags = 0;
            category = SymbolCategory.no_category;
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
            type = (Datatype)null;
            flags = 0;
            dispflags = 0;
            category = SymbolCategory.no_category;
            catindex = 0;
            symbolId = 0;
            wholeCount = 0;
            depthScope = (Scope)null;
            depthResolution = 0;
        }

        /// Get the local name of the symbol
        public string getName() => name;

        /// Get the name to display in output
        public string getDisplayName() => displayName;

        /// Get the data-type
        public Datatype? getType() => type;

        /// Get a unique id for the symbol
        public ulong getId() => symbolId;

        /// Get the boolean properties of the Symbol
        public Varnode.varnode_flags getFlags() => flags;

        /// Get the format to display the Symbol in
        public DisplayFlags getDisplayFormat() => (dispflags & DisplayFlags.MASK);

        /// Get the Symbol category
        public SymbolCategory getCategory() => category;

        /// Get the position of the Symbol within its category
        public ushort getCategoryIndex() => catindex;

        /// Is the Symbol type-locked
        public bool isTypeLocked() => ((flags & Varnode.varnode_flags.typelock) != 0);

        /// Is the Symbol name-locked
        public bool isNameLocked() => ((flags & Varnode.varnode_flags.namelock) != 0);

        /// Is the Symbol size type-locked
        public bool isSizeTypeLocked() => ((dispflags & DisplayFlags.size_typelock)!= 0);

        /// Is the Symbol volatile
        public bool isVolatile() => ((flags & Varnode.varnode_flags.volatil)!= 0);

        /// Is \b this the "this" pointer
        public bool isThisPointer() => ((dispflags & DisplayFlags.is_this_ptr)!= 0);

        /// Is storage really a pointer to the true Symbol
        public bool isIndirectStorage() => ((flags&Varnode.varnode_flags.indirectstorage)!= 0);

        /// Is this a reference to the function return value
        public bool isHiddenReturn() => ((flags & Varnode.varnode_flags.hiddenretparm) != 0);

        /// Does \b this have an undefined name
        public bool isNameUndefined()
        {
            return (name.Length == 15) && name.StartsWith("$$undef");
        }

        /// Does \b this have more than one \e entire mapping
        public bool isMultiEntry() => (wholeCount > 1);

        /// Were some SymbolEntrys not merged
        public bool hasMergeProblems() => ((dispflags & DisplayFlags.merge_problems)!= 0);

        /// Mark that some SymbolEntrys could not be merged
        public void setMergeProblems()
        {
            dispflags |= DisplayFlags.merge_problems;
        }

        public bool isIsolated() => ((dispflags & DisplayFlags.isolate)!= 0); ///< Return \b true if \b this is isolated from speculative merging

        /// Set whether \b this Symbol should be speculatively merged
        /// If the given value is \b true, any Varnodes that map directly to \b this Symbol,
        /// will not be speculatively merged with other Varnodes.  (Required merges will still happen).
        /// \param val is the given boolean value
        public void setIsolated(bool val)
        {
            if (val) {
                dispflags |= DisplayFlags.isolate;
                flags |= Varnode.varnode_flags.typelock;     // Isolated Symbol must be typelocked
                checkSizeTypeLock();
            }
            else
                dispflags &= ~(DisplayFlags.isolate);
        }

        /// Get the scope owning \b this Symbol
        public Scope getScope() => scope;

        /// Get the first entire mapping of the symbol
        /// \return the first SymbolEntry
        public SymbolEntry getFirstWholeMap()
        {
            if (mapentry.empty())
                throw new LowlevelError("No mapping for symbol: " + name);
            return mapentry[0];
        }

        /// Get first mapping of the symbol that contains the given Address
        /// This method may return a \e partial entry, where the SymbolEntry is only holding
        /// part of the whole Symbol.
        /// \param addr is an address within the desired storage location of the Symbol
        /// \return the first matching SymbolEntry
        public SymbolEntry? getMapEntry(Address addr)
        {
            SymbolEntry res;
            for (int i = 0; i < mapentry.size(); ++i) {
                res = mapentry[i];
                Address entryaddr = res.getAddr();
                if (addr.getSpace() != entryaddr.getSpace()) continue;
                if (addr.getOffset() < entryaddr.getOffset()) continue;
                int diff = (int)(addr.getOffset() - entryaddr.getOffset());
                if (diff >= res.getSize()) continue;
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
            int pos = 0;
            for (int i = 0; i < mapentry.size(); ++i)
            {
                SymbolEntry tmp = &(*mapentry[i]);
                if (tmp == entry)
                    return pos;
                if (entry.getSize() == type.getSize())
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
                Scope? point = scope;
                int count = 0;
                while (point != null) {
                    count += 1;
                    point = point.getParent();
                }
                return count - 1;   // Don't print global scope
            }
            if (depthScope == useScope)
                return depthResolution;
            depthScope = useScope;
            Scope distinguishScope = scope.findDistinguishingScope(useScope);
            depthResolution = 0;
            string distinguishName;
            Scope? terminatingScope;
            if (distinguishScope == null) {  // Symbol scope is ancestor of use scope
                distinguishName = name;
                terminatingScope = scope;
            }
            else {
                distinguishName = distinguishScope.getName();
                Scope currentScope = scope;
                while (currentScope != distinguishScope) {
                    // For any scope up to the distinguishing scope
                    depthResolution += 1;           // Print its name
                    currentScope = currentScope.getParent() ?? throw new BugException();
                }
                depthResolution += 1;       // Also print the distinguishing scope name
                terminatingScope = distinguishScope.getParent();
            }
            if (useScope.isNameUsed(distinguishName, terminatingScope))
                depthResolution += 1;       // Name was overridden, we need one more distinguishing name
            return depthResolution;
        }

        /// Encode basic Symbol properties as attributes
        /// \param encoder is the stream encoder
        public void encodeHeader(Sla.CORE.Encoder encoder)
        {
            encoder.writeString(AttributeId.ATTRIB_NAME, name);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_ID, getId());
            if ((flags & Varnode.varnode_flags.namelock) != 0)
                encoder.writeBool(AttributeId.ATTRIB_NAMELOCK, true);
            if ((flags & Varnode.varnode_flags.typelock) != 0)
                encoder.writeBool(AttributeId.ATTRIB_TYPELOCK, true);
            if ((flags & Varnode.varnode_flags.@readonly)!= 0)
                encoder.writeBool(AttributeId.ATTRIB_READONLY, true);
            if ((flags & Varnode.varnode_flags.volatil) != 0)
                encoder.writeBool(AttributeId.ATTRIB_VOLATILE, true);
            if ((flags & Varnode.varnode_flags.indirectstorage) != 0)
                encoder.writeBool(AttributeId.ATTRIB_INDIRECTSTORAGE, true);
            if ((flags & Varnode.varnode_flags.hiddenretparm) != 0)
                encoder.writeBool(AttributeId.ATTRIB_HIDDENRETPARM, true);
            if ((dispflags & DisplayFlags.isolate) != 0)
                encoder.writeBool(AttributeId.ATTRIB_MERGE, false);
            if ((dispflags & DisplayFlags.is_this_ptr) != 0)
                encoder.writeBool(AttributeId.ATTRIB_THISPTR, true);
            DisplayFlags format = getDisplayFormat();
            if (format != 0) {
                encoder.writeString(AttributeId.ATTRIB_FORMAT, Datatype.decodeIntegerFormat(format));
            }
            encoder.writeSignedInteger(AttributeId.ATTRIB_CAT, category);
            if (category >= 0)
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_INDEX, catindex);
        }

        /// Decode basic Symbol properties from a \<symbol> element
        /// \param decoder is the stream decoder
        public void decodeHeader(Sla.CORE.Decoder decoder)
        {
            name.clear();
            displayName.clear();
            category = no_category;
            symbolId = 0;
            while (true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_CAT) {
                    category = (short)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_FORMAT) {
                    dispflags |= Datatype.encodeIntegerFormat(decoder.readString());
                }
                else if (attribId == AttributeId.ATTRIB_HIDDENRETPARM) {
                    if (decoder.readBool())
                        flags |= Varnode.varnode_flags.hiddenretparm;
                }
                else if (attribId == AttributeId.ATTRIB_ID) {
                    symbolId = decoder.readUnsignedInteger();
                    if ((symbolId >> 56) == (ID_BASE >> 56))
                        symbolId = 0;       // Don't keep old internal id's
                }
                else if (attribId == AttributeId.ATTRIB_INDIRECTSTORAGE) {
                    if (decoder.readBool())
                        flags |= Varnode.varnode_flags.indirectstorage;
                }
                else if (attribId == AttributeId.ATTRIB_MERGE) {
                    if (!decoder.readBool()) {
                        dispflags |= isolate;
                        flags |= Varnode.varnode_flags.typelock;
                    }
                }
                else if (attribId == AttributeId.ATTRIB_NAME)
                    name = decoder.readString();
                else if (attribId == AttributeId.ATTRIB_NAMELOCK) {
                    if (decoder.readBool())
                        flags |= Varnode.varnode_flags.namelock;
                }
                else if (attribId == AttributeId.ATTRIB_READONLY) {
                    if (decoder.readBool())
                        flags |= Varnode.varnode_flags.@readonly;
                }
                else if (attribId == AttributeId.ATTRIB_TYPELOCK) {
                    if (decoder.readBool())
                        flags |= Varnode.varnode_flags.typelock;
                }
                else if (attribId == AttributeId.ATTRIB_THISPTR) {
                    if (decoder.readBool())
                        dispflags |= is_this_ptr;
                }
                else if (attribId == AttributeId.ATTRIB_VOLATILE) {
                    if (decoder.readBool())
                        flags |= Varnode.varnode_flags.volatil;
                }
                else if (attribId == AttributeId.ATTRIB_LABEL) {
                    displayName = decoder.readString();
                }
            }
            if (category == function_parameter) {
                catindex = decoder.readUnsignedInteger(AttributeId.ATTRIB_INDEX);
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
            type.encodeRef(encoder);
        }

        /// Decode details of the Symbol from a \<symbol> element
        /// \param decoder is the stream decoder
        public void decodeBody(Decoder decoder)
        {
            type = scope.getArch().types.decodeType(decoder);
            checkSizeTypeLock();
        }

        /// Encode \b this Symbol to a stream
        /// \param encoder is the stream encoder
        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_SYMBOL);
            encodeHeader(encoder);
            encodeBody(encoder);
            encoder.closeElement(ElementId.ELEM_SYMBOL);
        }

        /// Decode \b this Symbol from a stream
        /// Parse a Symbol from the next element in the stream
        /// \param decoder is the stream decoder
        public virtual void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_SYMBOL);
            decodeHeader(decoder);

            decodeBody(decoder);
            decoder.closeElement(elemId);
        }

        ///< Get number of bytes consumed within the address.symbol map
        /// Get the number of bytes consumed by a SymbolEntry representing \b this Symbol.
        /// By default, this is the number of bytes consumed by the Symbol's data-type.
        /// This gives the amount of leeway a search has when the address queried does not match
        /// the exact address of the Symbol. With functions, the bytes consumed by a SymbolEntry
        /// may not match the data-type size.
        /// \return the number of bytes in a default SymbolEntry
        public override int getBytesConsumed()
        {
            return type.getSize();
        }
    }
}
