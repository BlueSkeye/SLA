using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using System.Runtime.Intrinsics;

using ScopeMap = System.Collections.Generic.Dictionary<ulong, Sla.DECCORE.Scope>;

namespace Sla.DECCORE
{
    /// \brief A collection of Symbol objects within a single (namespace or functional) scope
    ///
    /// This acts as a traditional Symbol container, allowing them to be accessed by name, but
    /// it also keeps track of how a Symbol is mapped into memory. It allows a Symbol to be looked up
    /// by its location in memory, which is sensitive to the address of the code accessing the Symbol.
    ///
    /// Capabilities include:
    ///   - Search for Symbols
    ///      - By name
    ///      - By storage address
    ///      - By type of Symbol
    ///      - Containing a range
    ///      - Overlapping a range
    ///   - Insert or remove a Symbol
    ///   - Add or remove SymbolEntry objects which associate Symbols with storage and the code that accesses it
    ///   - Modify properties of a Symbol
    ///
    /// A scope also supports the idea of \b ownership of memory. In theory, for a Symbol in the scope, at
    /// the code locations where the Symbol storage is valid, the scope \e owns the storage memory. In practice,
    /// a Scope object knows about memory ranges where a Symbol might be \e discovered.  For instance, the
    /// global Scope usually owns all memory in the \e ram address space.
    internal abstract class Scope
    {
        //friend class Database;
        //friend class ScopeCompare;
        /// Range of data addresses \e owned by \b this scope
        internal RangeList rangetree;
        /// The parent scope
        private Scope? parent;
        /// Scope using \b this as a cache
        private Scope owner;
        /// Sorted list of child scopes
        private ScopeMap children;

        /// Attach a new child Scope to \b this
        /// Attach the child as an immediate sub-scope of \b this.
        /// Take responsibility of the child's memory: the child will be freed when this is freed.
        /// \param child is the Scope to make a child
        private void attachScope(Scope child)
        {
            child.parent = this;
            children[child.uniqueId] = child;  // uniqueId is guaranteed to be unique by Database
        }

        /// Detach a child Scope from \b this
        /// The indicated child Scope is deleted
        /// \param iter points to the Scope to delete
        private void detachScope(ScopeMap::iterator iter)
        {
            Scope* child = (*iter).second;
            children.erase(iter);
            delete child;
        }

        /// \brief Create a Scope id based on the scope's name and its parent's id
        /// Create a globally unique id for a scope simply from its name.
        /// \param baseId is the scope id of the parent scope
        /// \param nm is the name of scope
        /// \return the hash of the parent id and name
        internal static ulong hashScopeName(ulong baseId, string nm)
        {
            uint reg1 = (uint)(baseId >> 32);
            uint reg2 = (uint)baseId;
            reg1 = Globals.crc_update(reg1, 0xa9);
            reg2 = Globals.crc_update(reg2, reg1);
            for (int i = 0; i < nm.Length; ++i) {
                uint val = nm[i];
                reg1 = Globals.crc_update(reg1, val);
                reg2 = Globals.crc_update(reg2, reg1);
            }
            ulong res = reg1;
            res = (res << 32) | reg2;
            return res;
        }

        /// Architecture of \b this scope
        protected Architecture glb;
        /// Name of \b this scope
        protected string name;
        /// Name to display in output
        protected string displayName;
        /// (If non-null) the function which \b this is the local Scope for
        internal Funcdata? fd;
        /// Unique id for the scope, for deduping scope names, assigning symbol ids
        protected ulong uniqueId;

        /// \brief Query for Symbols starting at a given address, which match a given \b usepoint
        /// Searching starts at a first scope, continuing thru parents up to a second scope,
        /// which is not queried.  If a Scope \e controls the memory at that address, the Scope
        /// object is returned. Additionally, if a symbol matching the criterion is found,
        /// the matching SymbolEntry is passed back.
        /// \param scope1 is the first Scope where searching starts
        /// \param scope2 is the second Scope where searching ends
        /// \param addr is the given address to search for
        /// \param usepoint is the given point at which the memory is being accessed (can be an invalid address)
        /// \param addrmatch is used to pass-back any matching SymbolEntry
        /// \return the Scope owning the address or NULL if none found
        protected Scope? stackAddr(Scope scope1, Scope scope2, Address addr, Address usepoint,
            out SymbolEntry addrmatch)
        {
            SymbolEntry entry = new SymbolEntry();
            if (addr.isConstant()) return null;
            while ((scope1 != (Scope)null)&& (scope1 != scope2)) {
                entry = scope1.findAddr(addr, usepoint);
                if (entry != (SymbolEntry)null)
                {
                    *addrmatch = entry;
                    return scope1;
                }
                if (scope1.inScope(addr, 1, usepoint))
                    return scope1;      // Discovery of new variable
                scope1 = scope1.getParent();
            }
            return (Scope)null;
        }

        /// Query for a Symbol containing a given range which is accessed at a given \b usepoint
        /// Searching starts at a first scope, continuing thru parents up to a second scope,
        /// which is not queried.  If a Scope \e controls the memory in the given range, the Scope
        /// object is returned. If a known Symbol contains the range,
        /// the matching SymbolEntry is passed back.
        /// \param scope1 is the first Scope where searching starts
        /// \param scope2 is the second Scope where searching ends
        /// \param addr is the starting address of the given range
        /// \param size is the number of bytes in the given range
        /// \param usepoint is the point at which the memory is being accessed (can be an invalid address)
        /// \param addrmatch is used to pass-back any matching SymbolEntry
        /// \return the Scope owning the address or NULL if none found
        protected static Scope stackContainer(Scope scope1, Scope scope2, Address addr, int size,
            Address usepoint, out SymbolEntry addrmatch)
        {
            SymbolEntry* entry;
            if (addr.isConstant()) return (Scope)null;
            while ((scope1 != (Scope)null)&& (scope1 != scope2)) {
                entry = scope1.findContainer(addr, size, usepoint);
                if (entry != (SymbolEntry)null)
                {
                    *addrmatch = entry;
                    return scope1;
                }
                if (scope1.inScope(addr, size, usepoint))
                    return scope1;      // Discovery of new variable
                scope1 = scope1.getParent();
            }
            return (Scope)null;
        }

        /// Query for a Symbol which most closely matches a given range and \b usepoint
        ///
        /// Searching starts at a first scope, continuing thru parents up to a second scope,
        /// which is not queried.  If a Scope \e controls the memory in the given range, the Scope
        /// object is returned. Among symbols that overlap the given range, the SymbolEntry
        /// which most closely matches the starting address and size is passed back.
        /// \param scope1 is the first Scope where searching starts
        /// \param scope2 is the second Scope where searching ends
        /// \param addr is the starting address of the given range
        /// \param size is the number of bytes in the given range
        /// \param usepoint is the point at which the memory is being accessed (can be an invalid address)
        /// \param addrmatch is used to pass-back any matching SymbolEntry
        /// \return the Scope owning the address or NULL if none found
        protected Scope stackClosestFit(Scope scope1, Scope scope2, Address addr, int size,
            Address usepoint, out SymbolEntry addrmatch)
        {
            SymbolEntry* entry;
            if (addr.isConstant()) return (Scope)null;
            while ((scope1 != (Scope)null)&& (scope1 != scope2)) {
                entry = scope1.findClosestFit(addr, size, usepoint);
                if (entry != (SymbolEntry)null)
                {
                    *addrmatch = entry;
                    return scope1;
                }
                if (scope1.inScope(addr, size, usepoint))
                    return scope1;      // Discovery of new variable
                scope1 = scope1.getParent();
            }
            return (Scope)null;
        }

        /// Query for a function Symbol starting at the given address
        ///
        /// Searching starts at a first scope, continuing thru parents up to a second scope,
        /// which is not queried.  If a Scope \e controls the memory in the given range, the Scope
        /// object is returned. If a FunctionSymbol is found at the given address, the
        /// corresponding Funcdata object is passed back.
        /// \param scope1 is the first Scope where searching starts
        /// \param scope2 is the second Scope where searching ends
        /// \param addr is the given address where the function should start
        /// \param addrmatch is used to pass-back any matching function
        /// \return the Scope owning the address or NULL if none found
        protected static Scope stackFunction(Scope scope1, Scope scope2, Address addr,
            out Funcdata addrmatch)
        {
            Funcdata* fd;
            if (addr.isConstant()) return (Scope)null;
            while ((scope1 != (Scope)null)&& (scope1 != scope2)) {
                fd = scope1.findFunction(addr);
                if (fd != (Funcdata)null)
                {
                    *addrmatch = fd;
                    return scope1;
                }
                if (scope1.inScope(addr, 1, Address()))
                    return scope1;      // Discovery of new variable
                scope1 = scope1.getParent();
            }
            return (Scope)null;
        }

        /// Query for an \e external \e reference Symbol starting at the given address
        ///
        /// Searching starts at a first scope, continuing thru parents up to a second scope,
        /// which is not queried.  If a Scope \e controls the memory in the given range, the Scope
        /// object is returned. If an \e external \e reference is found at the address,
        /// pass back the matching ExternRefSymbol
        /// \param scope1 is the first Scope where searching starts
        /// \param scope2 is the second Scope where searching ends
        /// \param addr is the given address
        /// \param addrmatch is used to pass-back any matching Symbol
        /// \return the Scope owning the address or NULL if none found
        protected static Scope stackExternalRef(Scope scope1, Scope scope2, Address addr,
            out ExternRefSymbol addrmatch)
        {
            ExternRefSymbol* sym;
            if (addr.isConstant()) return (Scope)null;
            while ((scope1 != (Scope)null)&& (scope1 != scope2)) {
                sym = scope1.findExternalRef(addr);
                if (sym != (ExternRefSymbol)null)
                {
                    *addrmatch = sym;
                    return scope1;
                }
                // When searching for externalref, don't do discovery
                // As the function in a lower scope may be masking the
                // external reference symbol that refers to it
                //    if (scope1.inScope(addr,1,Address()))
                //      return scope1;		// Discovery of new variable
                scope1 = scope1.getParent();
            }
            return (Scope)null;
        }

        /// Query for a label Symbol for a given address.
        ///
        /// Searching starts at a first scope, continuing thru parents up to a second scope,
        /// which is not queried.  If a Scope \e controls the memory in the given range, the Scope
        /// object is returned. If there is a label at that address, pass back the
        /// corresponding LabSymbol object
        /// \param scope1 is the first Scope where searching starts
        /// \param scope2 is the second Scope where searching ends
        /// \param addr is the given address
        /// \param addrmatch is used to pass-back any matching Symbol
        /// \return the Scope owning the address or NULL if none found
        protected static Scope stackCodeLabel(Scope scope1, Scope scope2, Address addr,
            out LabSymbol addrmatch)
        {
            LabSymbol* sym;
            if (addr.isConstant()) return (Scope)null;
            while ((scope1 != (Scope)null)&& (scope1 != scope2)) {
                sym = scope1.findCodeLabel(addr);
                if (sym != (LabSymbol)null)
                {
                    *addrmatch = sym;
                    return scope1;
                }
                if (scope1.inScope(addr, 1, Address()))
                    return scope1;      // Discovery of new variable
                scope1 = scope1.getParent();
            }
            return (Scope)null;
        }

        /// Access the address ranges owned by \b this Scope
        protected RangeList getRangeTree() => rangetree;

        /// \brief Build an unattached Scope to be associated as a sub-scope of \b this
        /// This is a Scope object \e factory, intended to be called off of the global scope for building
        /// global namespace scopes.  Function scopes are handled differently.
        /// \param id is the globally unique id associated with the scope
        /// \param nm is the name of the new scope
        /// \return the new Scope object
        protected abstract Scope buildSubScope(ulong id, string nm);

        /// Convert \b this to a local Scope
        /// Attach \b this to the given function, which makes \b this the local scope for the function
        /// \param f is the given function to attach to
        protected virtual void restrictScope(Funcdata f)
        {
            fd = f;
        }

        // These add/remove range are for scope \b discovery, i.e. we may
        // know an address belongs to a certain scope, without knowing any symbol

        /// Add a memory range to the ownership of \b this Scope
        /// \param spc is the address space of the range
        /// \param first is the offset of the first byte in the range
        /// \param last is the offset of the last byte in the range
        protected virtual void addRange(AddrSpace spc, ulong first, ulong last)
        {
            rangetree.insertRange(spc, first, last);
        }

        /// Remove a memory range from the ownership of \b this Scope
        /// \param spc is the address space of the range
        /// \param first is the offset of the first byte in the range
        /// \param last is the offset of the last byte in the range
        protected virtual void removeRange(AddrSpace spc, ulong first, ulong last)
        {
            rangetree.removeRange(spc, first, last);
        }

        /// \brief Put a Symbol into the name map
        /// \param sym is the preconstructed Symbol
        protected abstract void addSymbolInternal(Symbol sym);

        /// \brief Create a new SymbolEntry for a Symbol given a memory range
        /// The SymbolEntry is specified in terms of a memory range and \b usepoint
        /// \param sym is the given Symbol being mapped
        /// \param exfl are any boolean Varnode properties specific to the memory range
        /// \param addr is the starting address of the given memory range
        /// \param off is the byte offset of the new SymbolEntry (relative to the whole Symbol)
        /// \param sz is the number of bytes in the range
        /// \param uselim is the given \b usepoint (which may be \e invalid)
        /// \return the newly created SymbolEntry
        protected abstract SymbolEntry addMapInternal(Symbol sym, uint exfl, Address addr,
            int off, int sz, RangeList uselim);

        /// \brief Create a new SymbolEntry for a Symbol given a dynamic hash
        /// The SymbolEntry is specified in terms of a \b hash and \b usepoint, which describe how
        /// to find the temporary Varnode holding the symbol value.
        /// \param sym is the given Symbol being mapped
        /// \param exfl are any boolean Varnode properties
        /// \param hash is the given dynamic hash
        /// \param off is the byte offset of the new SymbolEntry (relative to the whole Symbol)
        /// \param sz is the number of bytes occupied by the Varnode
        /// \param uselim is the given \b usepoint
        /// \return the newly created SymbolEntry
        protected abstract SymbolEntry addDynamicMapInternal(Symbol sym, uint exfl, ulong hash,
            int off, int sz, RangeList uselim);

        /// Integrate a SymbolEntry into the range maps
        /// The mapping is given as an unintegrated SymbolEntry object. Memory
        /// may be specified in terms of join addresses, which this method must unravel.
        /// The \b offset, \b size, and \b extraflags fields of the SymbolEntry are not used.
        /// In particular, the SymbolEntry is assumed to map the entire Symbol.
        /// \param entry is the given SymbolEntry
        /// \return a SymbolEntry which has been fully integrated
        protected SymbolEntry addMap(SymbolEntry entry)
        {
            // First set properties of this symbol based on scope
            //  entry.symbol.flags |= Varnode.varnode_flags.mapped;
            if (isGlobal())
                entry.symbol.flags |= Varnode.varnode_flags.persist;
            else if (!entry.addr.isInvalid())
            {
                // If this is not a global scope, but the address is in the global discovery range
                // we still mark the symbol as persistent
                Scope* glbScope = glb.symboltab.getGlobalScope();
                Address addr;
                if (glbScope.inScope(entry.addr, 1, addr))
                {
                    entry.symbol.flags |= Varnode.varnode_flags.persist;
                    entry.uselimit.clear(); // FIXME: Kludge for incorrectly generated XML
                }
            }

            SymbolEntry* res;
            int consumeSize = entry.symbol.getBytesConsumed();
            if (entry.addr.isInvalid())
                res = addDynamicMapInternal(entry.symbol, Varnode.varnode_flags.mapped, entry.hash, 0, consumeSize, entry.uselimit);
            else
            {
                if (entry.uselimit.empty())
                {
                    entry.symbol.flags |= Varnode.varnode_flags.addrtied;
                    // Global properties (like readonly and volatile)
                    // can only happen if use is not limited
                    entry.symbol.flags |= glb.symboltab.getProperty(entry.addr);
                }
                res = addMapInternal(entry.symbol, Varnode.varnode_flags.mapped, entry.addr, 0, consumeSize, entry.uselimit);
                if (entry.addr.isJoin())
                {
                    // The address is a join,  we add extra SymbolEntry maps for each of the pieces
                    JoinRecord* rec = glb.findJoin(entry.addr.getOffset());
                    uint exfl;
                    int num = rec.numPieces();
                    ulong off = 0;
                    bool bigendian = entry.addr.isBigEndian();
                    for (int j = 0; j < num; ++j)
                    {
                        int i = bigendian ? j : (num - 1 - j); // Take pieces in endian order
                        VarnodeData vdat = rec.getPiece(i);
                        if (i == 0)     // i==0 is most signif
                            exfl = Varnode.varnode_flags.precishi;
                        else if (i == num - 1)
                            exfl = Varnode.varnode_flags.precislo;
                        else
                            exfl = Varnode.varnode_flags.precislo | Varnode.varnode_flags.precishi; // Middle pieces have both flags set
                                                                          // NOTE: we do not turn on the mapped flag for the pieces
                        addMapInternal(entry.symbol, exfl, vdat.getAddr(), off, vdat.size, entry.uselimit);
                        off += vdat.size;
                    }
                    // Note: we fall thru here so that we return a SymbolEntry for the unified symbol
                }
            }
            return res;
        }

        /// Adjust the id associated with a symbol
        protected void setSymbolId(Symbol sym, ulong id)
        {
            sym.symbolId = id;
        }

        /// Change name displayed in output
        protected void setDisplayName(string nm)
        {
            displayName = nm;
        }

#if OPACTION_DEBUG
        public /*mutable*/ bool debugon;

        public void turnOnDebug()
        {
            debugon = true;
        }

        public void turnOffDebug()
        {
            debugon = false;
        }
#endif
        /// \brief Construct an empty scope, given a name and Architecture
        public Scope(ulong id, string nm, Architecture g, Scope own)
        {
            uniqueId = id;
            name = nm;
            displayName = nm;
            glb = g;
            parent = null;
            fd = null;
            owner = own;
#if OPACTION_DEBUG
            debugon = false;
#endif
        }

        ~Scope()
        {
            ScopeMap::iterator iter = children.begin();
            while (iter != children.end())
            {
                delete(*iter).second;
                ++iter;
            }
        }

        /// Beginning iterator to mapped SymbolEntrys
        public abstract IEnumerator<SymbolEntry> begin();

        ///// Ending iterator to mapped SymbolEntrys
        //public abstract MapIterator end();

        /// Beginning iterator to dynamic SymbolEntrys
        public abstract IEnumerator<SymbolEntry> beginDynamic();

        /// Ending iterator to dynamic SymbolEntrys
        public abstract IEnumerator<SymbolEntry> endDynamic();

        /// Beginning iterator to dynamic SymbolEntrys
        public abstract IEnumerator<SymbolEntry> beginDynamic();

        /// Ending iterator to dynamic SymbolEntrys
        public abstract IEnumerator<SymbolEntry> endDynamic();

        /// Clear all symbols from \b this scope
        public abstract void clear();

        /// Clear all symbols of the given category from \b this scope
        public abstract void clearCategory(int cat);

        /// Clear all unlocked symbols from \b this scope
        public abstract void clearUnlocked();

        /// Clear unlocked symbols of the given category from \b this scope
        public abstract void clearUnlockedCategory(int cat);

        /// \brief Let scopes internally adjust any caches
        ///
        /// This is called once after Architecture configuration is complete.
        public abstract void adjustCaches();

        /// \brief Query if the given range is owned by \b this Scope
        /// All bytes in the range must be owned, and ownership can be informed by
        /// particular code that is accessing the range.
        /// \param addr is the starting address of the range
        /// \param size is the number of bytes in the range
        /// \param usepoint is the code address at which the given range is being accessed (may be \e invalid)
        /// \return true if \b this Scope owns the memory range
        public bool inScope(Address addr, int size, Address usepoint)
        {
            return rangetree.inRange(addr, size);
        }

        /// Remove all SymbolEntrys from the given Symbol
        public abstract void removeSymbolMappings(Symbol symbol);

        /// Remove the given Symbol from \b this Scope
        public abstract void removeSymbol(Symbol symbol);

        /// Rename a Symbol within \b this Scope
        public abstract void renameSymbol(Symbol sym, string newname);

        /// \brief Change the data-type of a Symbol within \b this Scope
        /// If the size of the Symbol changes, any mapping (SymbolEntry) is adjusted
        /// \param sym is the given Symbol
        /// \param ct is the new data-type
        public abstract void retypeSymbol(Symbol sym, Datatype ct);

        /// Set boolean Varnode properties on a Symbol
        public abstract void setAttribute(Symbol sym, uint attr);

        /// Clear boolean Varnode properties on a Symbol
        public abstract void clearAttribute(Symbol sym, uint attr);

        /// Set the display format for a Symbol
        public abstract void setDisplayFormat(Symbol sym, uint attr);

        // Find routines only search the scope itself
        /// \brief Find a Symbol at a given address and \b usepoint
        /// \param addr is the given address
        /// \param usepoint is the point at which the Symbol is accessed (may be \e invalid)
        /// \return the matching SymbolEntry or NULL
        public abstract SymbolEntry findAddr(Address addr, Address usepoint);

        /// \brief Find the smallest Symbol containing the given memory range
        /// \param addr is the starting address of the given memory range
        /// \param size is the number of bytes in the range
        /// \param usepoint is the point at which the Symbol is accessed (may be \e invalid)
        /// \return the matching SymbolEntry or NULL
        public abstract SymbolEntry findContainer(Address addr, int size, Address usepoint);

        /// \brief Find Symbol which is the closest fit to the given memory range
        /// \param addr is the starting address of the given memory range
        /// \param size is the number of bytes in the range
        /// \param usepoint is the point at which the Symbol is accessed (may be \e invalid)
        /// \return the matching SymbolEntry or NULL
        public abstract SymbolEntry findClosestFit(Address addr, int size, Address usepoint);

        /// \brief Find the function starting at the given address
        /// \param addr is the given starting address
        /// \return the matching Funcdata object or NULL
        public abstract Funcdata findFunction(Address addr);

        /// \brief Find an \e external \e reference at the given address
        /// \param addr is the given address
        /// \return the matching ExternRefSymbol or NULL
        public abstract ExternRefSymbol findExternalRef(Address addr);

        /// \brief Find a label Symbol at the given address
        /// \param addr is the given address
        /// \return the matching LabSymbol or NULL
        public abstract LabSymbol findCodeLabel(Address addr);

        /// \brief Find first Symbol overlapping the given memory range
        /// \param addr is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \return an overlapping SymbolEntry or NULL if none exists
        public abstract SymbolEntry findOverlap(Address addr, int size);

        /// \brief Find a Symbol by name within \b this Scope
        /// If there are multiple Symbols with the same name, all are passed back.
        /// \param nm is the name to search for
        /// \param res will contain any matching Symbols
        public abstract void findByName(string nm, List<Symbol> res);

        /// \brief Check if the given name is occurs within the given scope path.
        /// Test for the presence of a symbol with the given name in either \b this scope or
        /// an ancestor scope up to but not including the given terminating scope.
        /// If the name is used \b true is returned.
        /// \param nm is the given name to test
        /// \param op2 is the terminating ancestor scope (or null)
        public abstract bool isNameUsed(string nm, Scope op2);

        /// \brief Convert an \e external \e reference to the referenced function
        /// \param sym is the Symbol marking the external reference
        /// \return the underlying Funcdata object or NULL if none exists
        public abstract Funcdata resolveExternalRefFunction(ExternRefSymbol sym);

        /// \brief Given an address and data-type, build a suitable generic symbol name
        /// \param addr is the given address
        /// \param pc is the address at which the name is getting used
        /// \param ct is a data-type used to inform the name
        /// \param index is a reference to an index used to make the name unique, which will be updated
        /// \param flags are boolean properties of the variable we need the name for
        /// \return the new variable name
        public abstract string buildVariableName(Address addr, Address pc, Datatype ct, int index,
            uint flags);

        /// \brief Build a formal \b undefined name, used internally when a Symbol is not given a name
        /// \return a special internal name that won't collide with other names in \b this Scope
        public abstract string buildUndefinedName();

        /// \brief Produce a version of the given symbol name that won't collide with other names in \b this Scope
        /// \param nm is the given name
        /// \return return a unique version of the name
        public abstract string makeNameUnique(string nm);

        ///< Encode \b this as a \<scope> element
        public abstract void encode(Sla.CORE.Encoder encoder);

        ///< Decode \b this Scope from a \<scope> element
        public abstract void decode(Sla.CORE.Decoder decoder);

        ///< Restore attributes for \b this Scope from wrapping element
        public void decodeWrappingAttributes(Sla.CORE.Decoder decoder)
        {
        }

        ///< Dump a description of all SymbolEntry objects to a stream
        public abstract void printEntries(TextWriter s);

        /// \brief Get the number of Symbols in the given category
        /// \param cat is the Symbol \e category
        /// \return the number in that \e category
        public abstract int getCategorySize(int cat);

        /// \brief Retrieve a Symbol by index within a specific \e category
        /// \param cat is the Symbol \e category
        /// \param ind is the index (within the category) of the Symbol
        /// \return the indicated Symbol or NULL if no Symbol with that index exists
        public abstract Symbol getCategorySymbol(int cat, int ind);

        /// \brief Set the \e category and index for the given Symbol
        /// \param sym is the given Symbol
        /// \param cat is the \e category to set for the Symbol
        /// \param ind is the index position to set (within the category)
        public abstract void setCategory(Symbol sym, int cat, int ind);

        /// \brief Add a new Symbol to \b this Scope, given a name, data-type, and a single mapping
        ///
        /// The Symbol object will be created with the given name and data-type.  A single mapping (SymbolEntry)
        /// will be created for the Symbol based on a given storage address for the symbol
        /// and an address for code that accesses the Symbol at that storage location.
        /// \param nm is the new name of the Symbol
        /// \param ct is the data-type of the new Symbol
        /// \param addr is the starting address of the Symbol storage
        /// \param usepoint is the point accessing that storage (may be \e invalid)
        /// \return the SymbolEntry matching the new mapping
        public SymbolEntry addSymbol(string nm, Datatype ct, Address addr, Address usepoint)
        {
            if (ct.hasStripped())
                ct = ct.getStripped() ?? throw new BugException();
            Symbol sym = new Symbol(owner, nm, ct);
            addSymbolInternal(sym);
            return addMapPoint(sym, addr, usepoint);
        }

        /// Get the name of the Scope
        public string getName() => name;

        /// Get name displayed in output
        public string getDisplayName() => displayName;

        /// Get the globally unique id
        public virtual ulong getId() => uniqueId;

        /// Return \b true if \b this scope is global
        public bool isGlobal() => (fd == null);

        // The main global querying routines

        /// Look-up symbols by name
        /// Starting from \b this Scope, look for a Symbol with the given name.
        /// If there are no Symbols in \b this Scope, recurse into the parent Scope.
        /// If there are 1 (or more) Symbols matching in \b this Scope, add them to
        /// the result list
        /// \param nm is the name to search for
        /// \param res is the result list
        public void queryByName(string nm, List<Symbol> res)
        {
            findByName(nm, res);
            if (!res.empty())
                return;
            if (parent != (Scope)null)
                parent.queryByName(nm, res);
        }

        /// Look-up a function by name
        /// Starting with \b this Scope, find a function with the given name.
        /// If there are no Symbols with that name in \b this Scope at all, recurse into the parent Scope.
        /// \param nm if the name to search for
        /// \return the Funcdata object of the matching function, or NULL if it doesn't exist
        public Funcdata? queryFunction(string nm)
        {
            List<Symbol> symList = new List<Symbol>();
            queryByName(nm, symList);
            for (int i = 0; i < symList.Count; ++i) {
                FunctionSymbol? funcsym = symList[i] as FunctionSymbol;
                if (funcsym != (FunctionSymbol)null)
                    return funcsym.getFunction();
            }
            return (Funcdata)null;
        }

        /// Get Symbol with matching address
        /// Within a sub-scope or containing Scope of \b this, find a Symbol
        /// that is mapped to the given address, where the mapping is valid at a specific \b usepoint.
        /// \param addr is the given address
        /// \param usepoint is the point at which code accesses that address (may be \e invalid)
        /// \return the matching SymbolEntry
        public SymbolEntry queryByAddr(Address addr, Address usepoint)
        {
            SymbolEntry? res = (SymbolEntry)null;
            Scope basescope = glb.symboltab.mapScope(this, addr, usepoint);
            stackAddr(basescope, (Scope)null, addr, usepoint, out res);
            return res;
        }

        ///< Find the smallest containing Symbol
        /// Within a sub-scope or containing Scope of \b this, find the smallest Symbol
        /// that contains a given memory range and can be accessed at a given \b usepoint.
        /// \param addr is the given starting address of the memory range
        /// \param size is the number of bytes in the range
        /// \param usepoint is a point at which the Symbol is accessed (may be \e invalid)
        /// \return the matching SymbolEntry or NULL
        public virtual SymbolEntry? queryContainer(Address addr, int size, Address usepoint)
        {
            SymbolEntry? res = (SymbolEntry)null;
            Scope basescope = glb.symboltab.mapScope(this, addr, usepoint);
            stackContainer(basescope, (Scope)null, addr, size, usepoint, out res);
            return res;
        }

        /// Find a Symbol or properties at the given address
        /// Similarly to queryContainer(), this searches for the smallest containing Symbol,
        /// but whether a known Symbol is found or not, boolean properties associated
        /// with the memory range are also search for and passed back.
        /// \param addr is the starting address of the range
        /// \param size is the number of bytes in the range
        /// \param usepoint is a point at which the memory range is accessed (may be \e invalid)
        /// \param flags is a reference used to pass back the boolean properties of the memory range
        /// \return the smallest SymbolEntry containing the range, or NULL
        public SymbolEntry? queryProperties(Address addr, int size, Address usepoint,
            out Varnode.varnode_flags flags)
        {
            SymbolEntry? res = (SymbolEntry)null;
            Scope basescope = glb.symboltab.mapScope(this, addr, usepoint);
            Scope finalscope = stackContainer(basescope, (Scope)null, addr, size, usepoint, out res);
            if (res != (SymbolEntry)null) // If we found a symbol
                flags = res.getAllFlags(); // use its flags
            else if (finalscope != (Scope)null) {
                // If we found just a scope
                // set flags just based on scope
                flags = Varnode.varnode_flags.mapped | Varnode.varnode_flags.addrtied;
                if (finalscope.isGlobal())
                    flags |= Varnode.varnode_flags.persist;
                flags |= glb.symboltab.getProperty(addr);
            }
            else
                flags = glb.symboltab.getProperty(addr);
            return res;
        }

        /// Look-up a function by address
        /// Within a sub-scope or containing Scope of \b this, find a function starting
        /// at the given address.
        /// \param addr is the starting address of the function
        /// \return the Funcdata object of the matching function, or NULL if it doesn't exist
        public Funcdata? queryFunction(Address addr)
        {
            Funcdata? res = (Funcdata)null;
            // We have no usepoint, so try to map from addr
            Scope basescope = glb.symboltab.mapScope(this, addr, new Address());
            stackFunction(basescope, (Scope)null, addr, out res);
            return res;
        }

        /// Look-up a function thru an \e external \e reference
        /// Given an address, search for an \e external \e reference. If no Symbol is
        /// found and \b this Scope does not own the address, recurse searching in the parent Scope.
        /// If an \e external \e reference is found, try to resolve the function it refers to
        /// and return it.
        /// \param addr is the given address where an \e external \e reference might be
        /// \return the referred to Funcdata object or NULL if not found
        public Funcdata? queryExternalRefFunction(Address addr)
        {
            ExternRefSymbol? sym = (ExternRefSymbol)null;
            // We have no usepoint, so try to map from addr
            Scope basescope = glb.symboltab.mapScope(this, addr, new Address());
            basescope = stackExternalRef(basescope, (Scope)null, addr, out sym);
            // Resolve the reference from the same scope we found the reference
            if (sym != (ExternRefSymbol)null)
                return basescope.resolveExternalRefFunction(sym);
            return (Funcdata)null;
        }

        /// Look-up a code label by address
        /// Within a sub-scope or containing Scope of \b this, find a label Symbol
        /// at the given address.
        /// \param addr is the given address
        /// \return the LabSymbol object, or NULL if it doesn't exist
        public LabSymbol? queryCodeLabel(Address addr)
        {
            LabSymbol res = (LabSymbol)null;
            // We have no usepoint, so try to map from addr
            Scope basescope = glb.symboltab.mapScope(this, addr, new Address());
            stackCodeLabel(basescope, (Scope)null, addr, out res);
            return res;
        }

        /// Find a child Scope of \b this
        /// Look for the immediate child of \b this with a given name
        /// \param nm is the child's name
        /// \param strategy is \b true if hash of the name determines id
        /// \return the child Scope or NULL if there is no child with that name
        public Scope? resolveScope(string nm, bool strategy)
        {
            Scope? result;

            if (strategy) {
                ulong key = hashScopeName(uniqueId, nm);
                if (!children.TryGetValue(key, out result)) return (Scope)null;
                if (null == result) throw new BugException();
                if (result.name == nm)
                    return result;
            }
            else if (nm.Length > 0 && nm[0] <= '9' && nm[0] >= '0') {
                // Allow the string to directly specify the id
                ulong key = ulong.Parse(nm);
                return (children.TryGetValue(key, out result)) ? result : (Scope)null;
            }
            else {
                foreach (Scope scope in children.Values) {
                    if (scope.name == nm)
                        return scope;
                }
            }
            return (Scope)null;
        }

        /// Find the owning Scope of a given memory range
        /// Discover a sub-scope or containing Scope of \b this, that \e owns the given
        /// memory range at a specific \b usepoint. Note that ownership does not necessarily
        /// mean there is a known symbol there.
        /// \param addr is the starting address of the memory range
        /// \param sz is the number of bytes in the range
        /// \param usepoint is a point at which the memory is getting accesses
        public Scope? discoverScope(Address addr, int sz, Address usepoint)
        {
            // Which scope "should" this range belong to
            if (addr.isConstant())
                return (Scope)null;
            Scope? basescope = glb.symboltab.mapScope(this, addr, usepoint);
            while (basescope != (Scope)null) {
                if (basescope.inScope(addr, sz, usepoint))
                    return basescope;
                basescope = basescope.getParent();
            }
            return (Scope)null;
        }

        /// Beginning iterator of child scopes
        public ScopeMap.Enumerator childrenBegin() => children.GetEnumerator();

        /// Ending iterator of child scopes
        // public ScopeMap::const_iterator childrenEnd() => children.end();

        ///< Encode all contained scopes to a stream
        /// This Scope and all of its sub-scopes are encoded as a sequence of \<scope> elements
        /// in post order.  For each Scope, the encode() method is invoked.
        /// \param encoder is the stream encoder
        /// \param onlyGlobal is \b true if only non-local Scopes should be saved
        public void encodeRecursive(Sla.CORE.Encoder encoder, bool onlyGlobal)
        {
            // Only save global scopes
            if (onlyGlobal && !isGlobal()) return;
            encode(encoder);
            foreach (Scope scope in children.Values) {
                scope.encodeRecursive(encoder, onlyGlobal);
            }
        }

        /// Change the data-type of a Symbol that is \e sizelocked
        /// Change (override) the data-type of a \e sizelocked Symbol, while preserving the lock.
        /// An exception is thrown if the new data-type doesn't fit the size.
        /// \param sym is the locked Symbol
        /// \param ct is the data-type to change the Symbol to
        public void overrideSizeLockType(Symbol sym, Datatype ct)
        {
            if (sym.type.getSize() == ct.getSize()) {
                if (!sym.isSizeTypeLocked())
                    throw new LowlevelError("Overriding symbol that is not size locked");
                sym.type = ct;
                return;
            }
            throw new LowlevelError("Overriding symbol with different type size");
        }

        /// Clear a Symbol's \e size-locked data-type
        /// Replace any overriding data-type type with the locked UNKNOWN type
        /// of the correct size. The data-type is \e cleared, but the lock is preserved.
        /// \param sym is the Symbol to clear
        public void resetSizeLockType(Symbol sym)
        {
            if (sym.type.getMetatype() == type_metatype.TYPE_UNKNOWN) return;   // Nothing to do
            int size = sym.type.getSize();
            sym.type = glb.types.getBase(size, type_metatype.TYPE_UNKNOWN);
        }

        /// Toggle the given Symbol as the "this" pointer
        public void setThisPointer(Symbol sym, bool val)
        {
            sym.setThisPointer(val);
        }

        /// Is this a sub-scope of the given Scope
        /// Does the given Scope contain \b this as a sub-scope.
        /// \param scp is the given Scope
        /// \return \b true if \b this is a sub-scope
        public bool isSubScope(Scope scp)
        {
            Scope tmp = this;
            do {
                if (tmp == scp) return true;
                tmp = tmp.parent;
            } while (tmp != (Scope)null);
            return false;
        }

        /// Get the full name of \b this Scope
        public string getFullName()
        {
            if (parent == (Scope)null) return "";
            string fname = name;
            Scope scope = parent;
            while (scope.parent != (Scope)null) {
                fname = scope.name + "::" + fname;
                scope = scope.parent;
            }
            return fname;
        }

        /// Get the ordered list of scopes up to \b this
        /// Put the parent scopes of \b this into an array in order, starting with the global scope.
        /// \param vec is storage for the array of scopes
        public void getScopePath(List<Scope> vec)
        {
            int count = 0;
            Scope? cur = this;
            while (cur != (Scope)null) {    // Count number of elements in path
                count += 1;
                cur = cur.parent;
            }
            vec.resize(count);
            cur = this;
            while (cur != (Scope)null) {
                count -= 1;
                vec[count] = cur;
                cur = cur.parent;
            }
        }

        /// Find first ancestor of \b this not shared by given scope
        /// Any two scopes share at least the \e global scope as a common ancestor. We find the first scope
        /// that is \e not in common.  The scope returned will always be an ancestor of \b this.
        /// If \b this is an ancestor of the other given scope, then null is returned.
        /// \param op2 is the other given Scope
        /// \return the first ancestor Scope that is not in common or null
        public Scope findDistinguishingScope(Scope op2)
        {
            if (this == op2) return (Scope)null;    // Quickly check most common cases
            if (parent == op2) return this;
            if (op2.parent == this) return (Scope)null;
            if (parent == op2.parent) return this;
            List<Scope> thisPath = new List<Scope>();
            List<Scope> op2Path = new List<Scope>();
            getScopePath(thisPath);
            op2.getScopePath(op2Path);
            int min = thisPath.size();
            if (op2Path.size() < min)
                min = op2Path.size();
            for (int i = 0; i < min; ++i)
            {
                if (thisPath[i] != op2Path[i])
                    return thisPath[i];
            }
            if (min < thisPath.size())
                return thisPath[min];   // thisPath matches op2Path but is longer
            if (min < op2Path.size())
                return (Scope)null; // op2Path matches thisPath but is longer
            return this;            // ancestor paths are identical (only base scopes differ)
        }

        /// Get the Architecture associated with \b this
        public Architecture getArch() => glb;

        /// Get the parent Scope (or NULL if \b this is the global Scope)
        public Scope? getParent() => parent;

        /// Add a new Symbol \e without mapping it to an address
        /// The Symbol is created and added to any name map, but no SymbolEntry objects are created for it.
        /// \param nm is the name of the new Symbol
        /// \param ct is a data-type to assign to the new Symbol
        /// \return the new Symbol object
        public Symbol addSymbol(string nm, Datatype ct)
        {
            Symbol sym = new Symbol(owner, nm, ct);
            addSymbolInternal(sym);     // Let this scope lay claim to the new object
            return sym;
        }

        /// Map a Symbol to a specific address
        /// Create a new SymbolEntry that maps the whole Symbol to the given address
        /// \param sym is the Symbol
        /// \param addr is the given address to map to
        /// \param usepoint is a point at which the Symbol is accessed at that address
        /// \return the SymbolEntry representing the new mapping
        public SymbolEntry addMapPoint(Symbol sym, Address addr, Address usepoint)
        {
            SymbolEntry entry = new SymbolEntry(sym);
            if (!usepoint.isInvalid())  // Restrict maps use if necessary
                entry.uselimit.insertRange(usepoint.getSpace(), usepoint.getOffset(), usepoint.getOffset());
            entry.addr = addr;
            return addMap(entry);
        }

        /// Parse a mapped Symbol from a \<mapsym> element
        /// A Symbol element is parsed first, followed by sequences of \<addr> elements or
        /// \<hash> and \<rangelist> elements which define 1 or more mappings of the Symbol.
        /// The new Symbol and SymbolEntry mappings are integrated into \b this Scope.
        /// \param decoder is the stream decoder
        /// \return the new Symbol
        public Symbol addMapSym(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_MAPSYM);
            uint subId = decoder.peekElement();
            Symbol sym;
            if (subId == ElementId.ELEM_SYMBOL)
                sym = new Symbol(owner);
            else if (subId == ElementId.ELEM_EQUATESYMBOL)
                sym = new EquateSymbol(owner);
            else if (subId == ElementId.ELEM_FUNCTION)
                sym = new FunctionSymbol(owner, glb.min_funcsymbol_size);
            else if (subId == ElementId.ELEM_FUNCTIONSHELL)
                sym = new FunctionSymbol(owner, glb.min_funcsymbol_size);
            else if (subId == ElementId.ELEM_LABELSYM)
                sym = new LabSymbol(owner);
            else if (subId == ElementId.ELEM_EXTERNREFSYMBOL)
                sym = new ExternRefSymbol(owner);
            else if (subId == ElementId.ELEM_FACETSYMBOL)
                sym = new UnionFacetSymbol(owner);
            else
                throw new LowlevelError("Unknown symbol type");
            try {
                // Protect against duplicate scope errors
                sym.decode(decoder);
            }
            catch (RecovError err) {
                delete sym;
                throw;
            }
            addSymbolInternal(sym); // This routine may throw, but it will delete sym in this case
            while (decoder.peekElement() != 0)
            {
                SymbolEntry entry(sym);
                entry.decode(decoder);
                if (entry.isInvalid())
                {
                    glb.printMessage("WARNING: Throwing out symbol with invalid mapping: " + sym.getName());
                    removeSymbol(sym);
                    decoder.closeElement(elemId);
                    return (Symbol)null;
                }
                addMap(entry);
            }
            decoder.closeElement(elemId);
            return sym;
        }

        /// \brief Create a function Symbol at the given address in \b this Scope
        ///
        /// The FunctionSymbol is created and mapped to the given address.
        /// A Funcdata object is only created once FunctionSymbol::getFunction() is called.
        /// \param addr is the entry address of the function
        /// \param nm is the name of the function, within \b this Scope
        /// \return the new FunctionSymbol object
        public FunctionSymbol addFunction(Address addr, string nm)
        {
            FunctionSymbol* sym;

            SymbolEntry* overlap = queryContainer(addr, 1, Address());
            if (overlap != (SymbolEntry)null)
            {
                string errmsg = "WARNING: Function " + name;
                errmsg += " overlaps object: " + overlap.getSymbol().getName();
                glb.printMessage(errmsg);
            }
            sym = new FunctionSymbol(owner, nm, glb.min_funcsymbol_size);
            addSymbolInternal(sym);
            // Map symbol to base address of function
            // there is no limit on the applicability of this map within scope
            addMapPoint(sym, addr, Address());
            return sym;
        }

        /// Create an \e external \e reference at the given address in \b this Scope
        ///
        /// An ExternRefSymbol is created and mapped to the given address and stores a reference
        /// address to the actual function.
        /// \param addr is the given address to map the Symbol to
        /// \param refaddr is the reference address
        /// \param nm is the name of the symbol/function
        /// \return the new ExternRefSymbol
        public ExternRefSymbol addExternalRef(Address addr, Address refaddr, string nm)
        {
            ExternRefSymbol* sym;

            sym = new ExternRefSymbol(owner, refaddr, nm);
            addSymbolInternal(sym);
            // Map symbol to given address
            // there is no limit on applicability of this map within scope
            SymbolEntry* ret = addMapPoint(sym, addr, Address());
            // Even if the external reference is in a readonly region, treat it as not readonly
            // As the value in the image probably isn't valid
            ret.symbol.flags &= ~((uint)Varnode.varnode_flags.@readonly);
            return sym;
        }

        /// \brief Create a code label at the given address in \b this Scope
        ///
        /// A LabSymbol is created and mapped to the given address.
        /// \param addr is the given address to map to
        /// \param nm is the name of the symbol/label
        /// \return the new LabSymbol
        public LabSymbol addCodeLabel(Address addr, string nm)
        {
            LabSymbol* sym;

            SymbolEntry* overlap = queryContainer(addr, 1, addr);
            if (overlap != (SymbolEntry)null)
            {
                string errmsg = "WARNING: Codelabel " + nm;
                errmsg += " overlaps object: " + overlap.getSymbol().getName();
                glb.printMessage(errmsg);
            }
            sym = new LabSymbol(owner, nm);
            addSymbolInternal(sym);
            addMapPoint(sym, addr, Address());
            return sym;
        }

        /// \brief Create a dynamically mapped Symbol attached to a specific data-flow
        ///
        /// The Symbol is created and mapped to a dynamic \e hash and a code address where
        /// the Symbol is being used.
        /// \param nm is the name of the Symbol
        /// \param ct is the data-type of the Symbol
        /// \param caddr is the code address where the Symbol is being used
        /// \param hash is the dynamic hash
        /// \return the new Symbol
        public Symbol addDynamicSymbol(string nm, Datatype ct, Address caddr, ulong hash)
        {
            Symbol* sym;

            sym = new Symbol(owner, nm, ct);
            addSymbolInternal(sym);
            RangeList rnglist;
            if (!caddr.isInvalid())
                rnglist.insertRange(caddr.getSpace(), caddr.getOffset(), caddr.getOffset());
            addDynamicMapInternal(sym, Varnode.varnode_flags.mapped, hash, 0, ct.getSize(), rnglist);
            return sym;
        }

        /// \brief Create a symbol that forces display conversion on a constant
        ///
        /// \param nm is the equate name to display, which may be empty for an integer conversion
        /// \param format is the type of integer conversion (Symbol::force_hex, Symbol::force_dec, etc.)
        /// \param value is the constant value being converted
        /// \param addr is the address of the p-code op reading the constant
        /// \param hash is the dynamic hash identifying the constant
        /// \return the new EquateSymbol
        public Symbol addEquateSymbol(string nm, uint format, ulong value, Address addr, ulong hash)
        {
            Symbol* sym;

            sym = new EquateSymbol(owner, nm, format, value);
            addSymbolInternal(sym);
            RangeList rnglist;
            if (!addr.isInvalid())
                rnglist.insertRange(addr.getSpace(), addr.getOffset(), addr.getOffset());
            addDynamicMapInternal(sym, Varnode.varnode_flags.mapped, hash, 0, 1, rnglist);
            return sym;
        }

        /// \brief Create a symbol forcing a field interpretation for a specific access to a variable with \e union data-type
        ///
        /// The symbol is attached to a specific Varnode and a PcodeOp that reads or writes to it.  The Varnode,
        /// in the context of the PcodeOp, is forced to have the data-type of the selected field, and field's name is used
        /// to represent the Varnode in output.
        /// \param nm is the name of the symbol
        /// \param dt is the union data-type containing the field to force
        /// \param fieldNum is the index of the desired field, or -1 if the whole union should be forced
        /// \param addr is the address of the p-code op reading/writing the Varnode
        /// \param hash is the dynamic hash identifying the Varnode
        /// \return the new UnionFacetSymbol
        public Symbol addUnionFacetSymbol(string nm, Datatype dt, int fieldNum, Address addr,
            ulong hash)
        {
            Symbol* sym = new UnionFacetSymbol(owner, nm, dt, fieldNum);
            addSymbolInternal(sym);
            RangeList rnglist;
            if (!addr.isInvalid())
                rnglist.insertRange(addr.getSpace(), addr.getOffset(), addr.getOffset());
            addDynamicMapInternal(sym, Varnode.varnode_flags.mapped, hash, 0, 1, rnglist);
            return sym;
        }

        /// Create a default name for the given Symbol
        /// Create default name given information in the Symbol and possibly a representative Varnode.
        /// This method extracts the crucial properties and then uses the buildVariableName method to
        /// construct the actual name.
        /// \param sym is the given Symbol to name
        /// \param base is an index (which may get updated) used to uniquify the name
        /// \param vn is an optional (may be null) Varnode representative of the Symbol
        /// \return the default name
        public string buildDefaultName(Symbol sym, int @base, Varnode vn)
        {
            if (vn != (Varnode)null && !vn.isConstant())
            {
                Address usepoint;
                if (!vn.isAddrTied() && fd != (Funcdata)null)
                    usepoint = vn.getUsePoint(*fd);
                HighVariable* high = vn.getHigh();
                if (sym.getCategory() == Symbol::function_parameter || high.isInput())
                {
                    int index = -1;
                    if (sym.getCategory() == Symbol::function_parameter)
                        index = sym.getCategoryIndex() + 1;
                    return buildVariableName(vn.getAddr(), usepoint, sym.getType(), index, vn.getFlags() | Varnode.varnode_flags.input);
                }
                return buildVariableName(vn.getAddr(), usepoint, sym.getType(), base, vn.getFlags());
            }
            if (sym.numEntries() != 0)
            {
                SymbolEntry* entry = sym.getMapEntry(0);
                Address addr = entry.getAddr();
                Address usepoint = entry.getFirstUseAddress();
                uint flags = usepoint.isInvalid() ? Varnode.varnode_flags.addrtied : 0;
                if (sym.getCategory() == Symbol::function_parameter)
                {
                    flags |= Varnode.varnode_flags.input;
                    int index = sym.getCategoryIndex() + 1;
                    return buildVariableName(addr, usepoint, sym.getType(), index, flags);
                }
                return buildVariableName(addr, usepoint, sym.getType(), base, flags);
            }
            // Should never reach here
            return buildVariableName(Address(), Address(), sym.getType(), base, 0);
        }

        /// \brief Is the given memory range marked as \e read-only
        ///
        /// Check for Symbols relative to \b this Scope that are marked as \e read-only,
        /// and look-up properties of the memory in general.
        /// \param addr is the starting address of the given memory range
        /// \param size is the number of bytes in the range
        /// \param usepoint is a point where the range is getting accessed
        /// \return \b true if the memory is marked as \e read-only
        public bool isReadOnly(Address addr, int size, Address usepoint)
        {
            uint flags;
            queryProperties(addr, size, usepoint, flags);
            return ((flags & Varnode.varnode_flags.@readonly)!= 0);
        }

        /// Print a description of \b this Scope's \e owned memory ranges
        public void printBounds(TextWriter s)
        {
            rangetree.printBounds(s);
        }
    }
}
