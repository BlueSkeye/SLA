using Sla.CORE;
using Sla.EXTRA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

using ScopeMap = System.Collections.Generic.Dictionary<ulong, Sla.DECCORE.Scope>;
using ScopeResolve = Sla.EXTRA.rangemap<Sla.DECCORE.ScopeMapper>;

namespace Sla.DECCORE
{
    /// \brief A manager for symbol scopes for a whole executable
    /// This is the highest level container for anything related to Scope and Symbol
    /// objects, it indirectly holds the Funcdata objects as well, through the FunctionSymbol.
    /// It acts as the formal \b symbol \b table for the decompiler. The API is mostly concerned
    /// with the management of Scope objects.
    /// A Scope object is initially registered via attachScope(), then it can looked up by name.
    /// This class maintains the cross Scope search by address capability, implemented as a
    /// map from an Address to the Scope that owns it.  For efficiency, this map is really
    /// only applied to \e namespace Scopes, the global Scope and function Scopes are not
    /// entered in the map.  This class also maintains a set of boolean properties that label
    /// memory ranges.  This allows important properties like \e read-only and \e volatile to
    /// be put down even if the Symbols aren't yet known.
    internal class Database
    {
        /// Architecture to which this symbol table is attached
        private Architecture glb;
        /// Quick reference to the \e global Scope
        private Scope globalscope;
        /// Address to \e namespace map
        private ScopeResolve resolvemap;
        /// Map from id to Scope
        private ScopeMap idmap;
        /// Map of global properties
        private partmap<Address, Varnode.varnode_flags> flagbase;
        /// True if scope ids are built from hash of name
        private bool idByNameHash;

        /// Clear the \e ownership ranges associated with the given Scope
        /// Check to make sure the Scope is a \e namespace then remove all
        /// its address ranges from the map.
        /// \param scope is the given Scope
        private void clearResolve(Scope scope)
        {
            if (scope == globalscope) return;       // Does not apply to the global scope
            if (scope.fd != (Funcdata)null) return;  // Does not apply to functional scopes

            IEnumerator<Sla.CORE.Range> iter = scope.rangetree.begin();

            while (iter.MoveNext()) {
                Sla.CORE.Range rng = iter.Current;
                pair<ScopeResolve::const_iterator, ScopeResolve::const_iterator> res;
                res = resolvemap.find(rng.getFirstAddr());
                while (res.first != res.second)
                {
                    if ((*res.first).scope == scope)
                    {
                        resolvemap.erase(res.first);
                        break;
                    }
                }
            }
        }

        /// Clear any map references to the given Scope and its children
        /// This recursively clears references in idmap or in resolvemap.
        /// \param scope is the given Scope to clear
        private void clearReferences(Scope scope)
        {
            ScopeMap::const_iterator iter = scope.children.begin();
            ScopeMap::const_iterator enditer = scope.children.end();
            while (iter != enditer)
            {
                clearReferences((*iter).second);
                ++iter;
            }
            idmap.erase(scope.uniqueId);
            clearResolve(scope);
        }

        /// Add the \e ownership ranges of the given Scope to the map
        /// If the Scope is a \e namespace, iterate through all its ranges, adding each to the map
        /// \param scope is the given Scope to add
        private void fillResolve(Scope scope)
        {
            if (scope == globalscope) return;       // Does not apply to the global scope
            if (scope.fd != (Funcdata)null) return;  // Does not apply to functional scopes

            IEnumerator<Sla.CORE.Range> iter = scope.rangetree.begin();
            while (iter.MoveNext()) {
                Sla.CORE.Range rng = iter.Current;
                resolvemap.insert(scope, rng.getFirstAddr(), rng.getLastAddr());
            }
        }

        /// Figure out parent scope given \<parent> tag.
        /// Parse a \<parent> element for the scope id of the parent namespace.
        /// Look up the parent scope and return it.
        /// Throw an error if there is no matching scope
        /// \param decoder is the stream decoder
        /// \return the matching scope
        private Scope parseParentTag(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_PARENT);
            ulong id = decoder.readUnsignedInteger(AttributeId.ATTRIB_ID);
            Scope* res = resolveScope(id);
            if (res == (Scope)null)
                throw new CORE.LowlevelError("Could not find scope matching id");
            decoder.closeElement(elemId);
            return res;
        }

        /// Constructor
        /// Initialize a new symbol table, with no initial scopes or symbols.
        /// \param g is the Architecture that owns the symbol table
        /// \param idByName is \b true if scope ids are calculated as a hash of the scope name.
        public Database(Architecture g, bool idByName)
        {
            glb = g;
            globalscope = (Scope)null;
            flagbase.defaultValue() = 0;
            idByNameHash = idByName;
        }

        ~Database()
        {
            if (globalscope != (Scope)null)
                deleteScope(globalscope);
        }

        /// Get the Architecture associate with \b this
        public Architecture getArch() => glb;

        /// Let scopes adjust after configuration is finished
        /// Give \b this database the chance to inform existing scopes of any change to the
        /// configuration, which may have changed since the initial scopes were created.
        public void adjustCaches()
        {
            ScopeMap::iterator iter;
            for (iter = idmap.begin(); iter != idmap.end(); ++iter)
            {
                (*iter).second.adjustCaches();
            }
        }

        /// Register a new Scope
        /// The new Scope must be initially empty and \b this Database takes over ownership.
        /// Practically, this is just setting up the new Scope as a sub-scope of its parent.
        /// The parent Scope should already be registered with \b this Database, or
        /// NULL can be passed to register the global Scope.
        /// \param newscope is the new Scope being registered
        /// \param parent is the parent Scope or NULL
        public void attachScope(Scope newscope, Scope parent)
        {
            if (parent == (Scope)null)
            {
                if (globalscope != (Scope)null)
                    throw new CORE.LowlevelError("Multiple global scopes");
                if (newscope.name.size() != 0)
                    throw new CORE.LowlevelError("Global scope does not have empty name");
                globalscope = newscope;
                idmap[globalscope.uniqueId] = globalscope;
                return;
            }
            if (newscope.name.size() == 0)
                throw new CORE.LowlevelError("Non-global scope has empty name");
            pair<ScopeMap::iterator, bool> res;
            res = idmap.Add(newscope.uniqueId, newscope);
            if (res.second == false) {
                StringWriter s = new StringWriter();
                s.Write($"Duplicate scope id: {newscope.getFullName()}");
                // delete newscope;
                throw new RecovError(s.ToString());
            }
            parent.attachScope(newscope);
        }

        /// Delete the given Scope and all its sub-scopes
        /// \param scope is the given Scope
        public void deleteScope(Scope scope)
        {
            clearReferences(scope);
            if (globalscope == scope)
            {
                globalscope = (Scope)null;
                delete scope;
            }
            else
            {
                ScopeMap::iterator iter = scope.parent.children.find(scope.uniqueId);
                if (iter == scope.parent.children.end())
                    throw new CORE.LowlevelError("Could not remove parent reference to: " + scope.name);
                scope.parent.detachScope(iter);
            }
        }

        /// Delete all sub-scopes of the given Scope
        /// The given Scope is not deleted, only its children.
        /// \param scope is the given Scope
        public void deleteSubScopes(Scope scope)
        {
            ScopeMap::iterator iter = scope.children.begin();
            ScopeMap::iterator enditer = scope.children.end();
            ScopeMap::iterator curiter;
            while (iter != enditer)
            {
                curiter = iter;
                ++iter;
                clearReferences((*curiter).second);
                scope.detachScope(curiter);
            }
        }

        /// Clear unlocked Symbols owned by the given Scope
        /// All unlocked symbols in \b this Scope, and recursively into its sub-scopes,
        /// are removed.
        /// \param scope is the given Scope
        public void clearUnlocked(Scope scope)
        {
            ScopeMap::iterator iter = scope.children.begin();
            ScopeMap::iterator enditer = scope.children.end();
            while (iter != enditer)
            {
                Scope* subscope = (*iter).second;
                clearUnlocked(subscope);
                ++iter;
            }
            scope.clearUnlocked();
        }

        /// Set the \e ownership range for a Scope
        /// Any existing \e ownership is completely replaced.  The address to Scope map is updated.
        /// \param scope is the given Scope
        /// \param rlist is the set of addresses to mark as owned
        public void setRange(Scope scope, RangeList rlist)
        {
            clearResolve(scope);
            scope.rangetree = rlist;   // Overwrite whole tree
            fillResolve(scope);
        }

        /// Add an address range to the \e ownership of a Scope
        /// The new range will be merged with the existing \e ownership.
        /// The address to Scope map is updated
        /// \param scope is the given Scope
        /// \param spc is the address space of the memory range being added
        /// \param first is the offset of the first byte in the array
        /// \param last is the offset of the last byte
        public void addRange(Scope scope, AddrSpace spc, ulong first, ulong last)
        {
            clearResolve(scope);
            scope.addRange(spc, first, last);
            fillResolve(scope);
        }

        /// Remove an address range from \e ownership of a Scope
        /// Addresses owned by the Scope that are disjoint from the given range are
        /// not affected.
        /// \param scope is the given Scope
        /// \param spc is the address space of the memory range being removed
        /// \param first is the offset of the first byte in the array
        /// \param last is the offset of the last byte
        public void removeRange(Scope scope, AddrSpace spc, ulong first, ulong last)
        {
            clearResolve(scope);
            scope.removeRange(spc, first, last);
            fillResolve(scope);
        }

        /// Get the global Scope
        public Scope getGlobalScope() => globalscope;

        /// Look-up a Scope by id
        /// Find a Scope object, given its global id.  Return null if id is not mapped to a Scope.
        /// \param id is the global id
        /// \return the matching Scope or null
        public Scope resolveScope(ulong id)
        {
            ScopeMap::const_iterator iter = idmap.find(id);
            if (iter != idmap.end())
                return (*iter).second;
            return (Scope)null;
        }

        /// \brief Get the Scope (and base name) associated with a qualified Symbol name
        ///
        /// The name is parsed using a \b delimiter that is passed @in. The name can
        /// be only partially qualified by passing in a starting Scope, which the
        /// name is assumed to be relative to. If the starting scope is \b null, or the name
        /// starts with the delimiter, the name is assumed to be relative to the global Scope.
        /// The unqualified (base) name of the Symbol is passed back to the caller.
        /// \param fullname is the qualified Symbol name
        /// \param delim is the delimiter separating names
        /// \param basename will hold the passed back base Symbol name
        /// \param start is the Scope to start drilling down from, or NULL for the global scope
        /// \return the Scope being referred to by the name
        public Scope? resolveScopeFromSymbolName(string fullname, string delim, out string basename,
            Scope? start)
        {
            if (start == (Scope)null)
                start = globalscope;

            int mark = 0;
            int endmark;
            while (true) {
                endmark = fullname.IndexOf(delim, mark);
                if (-1 == endmark) break;
                if (endmark == 0) {
                    // Path is "absolute"
                    start = globalscope;    // Start from the global scope
                }
                else {
                    string scopename = fullname.Substring(mark, endmark - mark);
                    start = start.resolveScope(scopename, idByNameHash);
                    if (start == (Scope)null) // Was the scope name bad
                        return (Scope)null;
                }
                mark = endmark + delim.Length;
            }
            basename = fullname.Substring(mark, endmark);
            return start;
        }

        /// Find (and if not found create) a specific subscope
        /// Look for a Scope by id.  If it does not exist, create a new scope
        /// with the given name and parent scope.
        /// \param id is the global id of the Scope
        /// \param nm is the given name of the Scope
        /// \param parent is the given parent scope to search
        /// \return the subscope object either found or created
        public Scope findCreateScope(ulong id, string nm, Scope parent)
        {
            Scope? res = resolveScope(id);
            if (res != (Scope)null)
                return res;
            res = globalscope.buildSubScope(id, nm);
            attachScope(res, parent);
            return res;
        }

        /// \brief Find and/or create Scopes associated with a qualified Symbol name
        ///
        /// The name is parsed using a \b delimiter that is passed @in. The name can
        /// be only partially qualified by passing in a starting Scope, which the
        /// name is assumed to be relative to. Otherwise the name is assumed to be
        /// relative to the global Scope.  The unqualified (base) name of the Symbol
        /// is passed back to the caller.  Any missing scope in the path is created.
        /// \param fullname is the qualified Symbol name
        /// \param delim is the delimiter separating names
        /// \param basename will hold the passed back base Symbol name
        /// \param start is the Scope to start drilling down from, or NULL for the global scope
        /// \return the Scope being referred to by the name
        public Scope findCreateScopeFromSymbolName(string fullname, string delim, out string basename,
            Scope? start)
        {
            if (start == (Scope)null)
                start = globalscope;

            int mark = 0;
            int endmark;
            while (true) {
                endmark = fullname.IndexOf(delim, mark);
                if (-1 == endmark) break;
                if (!idByNameHash)
                    throw new CORE.LowlevelError("Scope name hashes not allowed");
                string scopename = fullname.Substring(mark, endmark - mark);
                ulong nameId = Scope.hashScopeName(start.uniqueId, scopename);
                start = findCreateScope(nameId, scopename, start);
                mark = endmark + delim.Length;
            }
            basename = fullname.Substring(mark, endmark);
            return start;
        }

        /// \brief Determine the lowest-level Scope which might contain the given address as a Symbol
        ///
        /// As currently implemented, this method can only find a \e namespace Scope.
        /// When searching for a Symbol by Address, the global Scope is always
        /// searched because it is the terminating Scope when recursively walking scopes through
        /// the \e parent relationship, so it isn't entered in this map.  A function level Scope,
        /// also not entered in the map, is only returned as the Scope passed in as a default,
        /// when no \e namespace Scope claims the address.
        /// \param qpoint is the default Scope returned if no \e owner is found
        /// \param addr is the address whose owner should be searched for
        /// \param usepoint is a point in code where the address is being accessed (may be \e invalid)
        /// \return a Scope to act as a starting point for a hierarchical search
        public Scope mapScope(Scope qpoint, Address addr, Address usepoint)
        {
            if (resolvemap.empty()) // If there are no namespace scopes
                return qpoint;      // Start querying from scope placing query
            pair<ScopeResolve::const_iterator, ScopeResolve::const_iterator> res;
            res = resolvemap.find(addr);
            if (res.first != res.second)
                return (*res.first).getScope();
            return qpoint;
        }

        /// Get boolean properties at the given address
        public Varnode.varnode_flags getProperty(Address addr) => flagbase.getValue(addr);

        /// Set boolean properties over a given memory range
        /// This allows the standard boolean Varnode properties like
        /// \e read-only and \e volatile to be put an a memory range, independent
        /// of whether a Symbol is there or not.  These get picked up by the
        /// Scope::queryProperties() method in particular.
        /// \param flags is the set of boolean properties
        /// \param range is the memory range to label
        public void setPropertyRange(Varnode.varnode_flags flags, Sla.CORE.Range range)
        {
            Address addr1 = range.getFirstAddr();
            Address addr2 = range.getLastAddrOpen(glb);
            flagbase.split(addr1);
            // WARNING : The returned enumerator is already set on the first relevant record.
            // DO NOT use MoveNext before reading
            IEnumerator<KeyValuePair<Address, uint>> aiter = flagbase.begin(addr1) ?? throw new BugException();
            IEnumerator<KeyValuePair<Address, uint>>? biter;

            if (!addr2.isInvalid()) {
                flagbase.split(addr2);
                biter = flagbase.begin(addr2);
            }
            else
                biter = null;
            while (true) {
                if ((null != biter) && (aiter.Current.Key == biter.Current.Key)) {
                    break;
                }
                // Update bits across whole range
                flagbase.updateValue(aiter.Current.Key, aiter.Current.Value | flags);
                if (!aiter.MoveNext()) {
                    break;
                }
            }
        }

        /// Clear boolean properties over a given memory range
        /// The non-zero bits in the \b flags parameter indicate the boolean properties to be cleared.
        /// No other properties are altered.
        /// \param flags is the set of properties to clear
        /// \param range is the memory range to clear
        public void clearPropertyRange(Varnode.varnode_flags flags, Sla.CORE.Range range)
        {
            Address addr1 = range.getFirstAddr();
            Address addr2 = range.getLastAddrOpen(glb);
            flagbase.split(addr1);
            IEnumerator<KeyValuePair<Address, uint>>? biter;

            // WARNING : The returned enumerator is already set on the first relevant record.
            // DO NOT use MoveNext before reading
            IEnumerator<KeyValuePair<Address, uint>> aiter = flagbase.begin(addr1) ?? throw new BugException();
            if (!addr2.isInvalid()) {
                flagbase.split(addr2);
                biter = flagbase.begin(addr2);
            }
            else
                biter = null;
            flags = ~flags;
            while (true) {
                if ((null != biter) && (aiter.Current.Key == biter.Current.Key)) {
                    break;
                }
                // Update bits across whole range
                flagbase.updateValue(aiter.Current.Key, aiter.Current.Value & flags);
                if (!aiter.MoveNext()) {
                    break;
                }
            }
        }

        public void setProperties(partmap<Address, uint> newflags)
        {
            flagbase = newflags;
        }    ///< Replace the property map

        /// Get the entire property map
        public partmap<Address, uint> getProperties() => flagbase;

        /// Encode the whole Database to a stream
        /// Encode a single \<db> element to the stream, which contains child elements
        /// for each Scope (which contain Symbol children in turn).
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_DB);
            if (idByNameHash)
                encoder.writeBool(AttributeId.ATTRIB_SCOPEIDBYNAME, true);
            // Save the property change points
            IEnumerator<KeyValuePair<Address, uint>> piter = flagbase.begin() ?? throw new BugException();
            while (piter.MoveNext()) {
                Address addr = piter.Current.Key;
                uint val = piter.Current.Value;
                encoder.openElement(ElementId.ELEM_PROPERTY_CHANGEPOINT);
                addr.getSpace().encodeAttributes(encoder, addr.getOffset());
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_VAL, val);
                encoder.closeElement(ElementId.ELEM_PROPERTY_CHANGEPOINT);
            }

            if (globalscope != (Scope)null)
                globalscope.encodeRecursive(encoder, true);        // Save the global scopes
            encoder.closeElement(ElementId.ELEM_DB);
        }

        /// Decode the whole database from a stream
        /// Parse a \<db> element to recover Scope and Symbol objects.
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_DB);
            idByNameHash = false;       // Default
            while(true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_SCOPEIDBYNAME)
                    idByNameHash = decoder.readBool();
            }
            while(true) {
                uint subId = decoder.peekElement();
                if (subId != ElementId.ELEM_PROPERTY_CHANGEPOINT) break;
                decoder.openElement();
                uint val = (uint)decoder.readUnsignedInteger(AttributeId.ATTRIB_VAL);
                VarnodeData vData = VarnodeData.decodeFromAttributes(decoder);
                Address addr = vData.getAddr();
                decoder.closeElement(subId);
                flagbase.split(addr) = val;
            }

            while(true) {
                uint subId = decoder.openElement();
                if (subId != ElementId.ELEM_SCOPE) break;
                string name;
                string displayName;
                ulong id = 0;
                bool seenId = false;
                while(true) {
                    AttributeId attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == AttributeId.ATTRIB_NAME)
                        name = decoder.readString();
                    else if (attribId == AttributeId.ATTRIB_ID) {
                        id = decoder.readUnsignedInteger();
                        seenId = true;
                    }
                    else if (attribId == AttributeId.ATTRIB_LABEL)
                        displayName = decoder.readString();
                }
                if (name.empty() || !seenId)
                    throw DecoderError("Missing name and id attributes in scope");
                Scope* parentScope = (Scope)null;
                uint parentId = decoder.peekElement();
                if (parentId == ELEM_PARENT)
                {
                    parentScope = parseParentTag(decoder);
                }
                Scope* newScope = findCreateScope(id, name, parentScope);
                if (!displayName.empty())
                    newScope.setDisplayName(displayName);
                newScope.decode(decoder);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        /// Register and fill out a single Scope from an XML \<scope> tag
        /// This allows incremental building of the Database from multiple stream sources.
        /// An empty Scope must already be allocated.  It is registered with \b this Database,
        /// and then populated with Symbol objects based as the content of a given element.
        /// The element can either be a \<scope> itself, or another element that wraps a \<scope>
        /// element as its first child.
        /// \param decoder is the stream decoder
        /// \param newScope is the empty Scope
        public void decodeScope(Decoder decoder, Scope newScope)
        {
            uint elemId = decoder.openElement();
            if (elemId == ELEM_SCOPE)
            {
                Scope* parentScope = parseParentTag(decoder);
                attachScope(newScope, parentScope);
                newScope.decode(decoder);
            }
            else
            {
                newScope.decodeWrappingAttributes(decoder);
                uint subId = decoder.openElement(ElementId.ELEM_SCOPE);
                Scope* parentScope = parseParentTag(decoder);
                attachScope(newScope, parentScope);
                newScope.decode(decoder);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        /// Decode a namespace path and make sure each namespace exists
        /// Some namespace objects may already exist.  Create those that don't.
        /// \param decoder is the stream to decode the path from
        /// \return the namespace described by the path
        public Scope decodeScopePath(Decoder decoder)
        {
            Scope curscope = getGlobalScope();
            uint elemId = decoder.openElement(ElementId.ELEM_PARENT);
            uint subId = decoder.openElement();
            decoder.closeElementSkipping(subId);        // Skip element describing the root scope
            while(true) {
                subId = decoder.openElement();
                if (subId != ElementId.ELEM_VAL) break;
                string displayName = string.Empty;
                ulong scopeId = 0;
                while(true) {
                    AttributeId attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == AttributeId.ATTRIB_ID)
                        scopeId = decoder.readUnsignedInteger();
                    else if (attribId == AttributeId.ATTRIB_LABEL)
                        displayName = decoder.readString();
                }
                string name = decoder.readString(AttributeId.ATTRIB_CONTENT);
                if (scopeId == 0)
                    throw new DecoderError("Missing name and id in scope");
                curscope = findCreateScope(scopeId, name, curscope);
                if (!displayName.empty())
                    curscope.setDisplayName(displayName);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
            return curscope;
        }
    }
}
