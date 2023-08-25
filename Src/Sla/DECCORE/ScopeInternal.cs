using Sla.CORE;
using Sla.DECCORE;
using Sla.EXTRA;
using System.Globalization;

using EntryMap = Sla.EXTRA.rangemap<Sla.DECCORE.SymbolEntry>;

namespace Sla.DECCORE
{
    /// \brief An in-memory implementation of the Scope interface.
    /// This can act as a stand-alone Scope object or serve as an in-memory cache for
    /// another implementation.  This implements a \b nametree, which is a
    /// a set of Symbol objects (the set owns the Symbol objects). It also implements
    /// a \b maptable, which is a list of rangemaps that own the SymbolEntry objects.
    internal class ScopeInternal : Scope
    {
        /// \brief Parse a \<hole> element describing boolean properties of a memory range.
        /// The \<scope> element is allowed to contain \<hole> elements, which are really descriptions
        /// of memory globally. This method parses them and passes the info to the Database
        /// object.
        /// \param decoder is the stream decoder
        private void decodeHole(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_HOLE);
            Varnode.varnode_flags flags = 0;
            Sla.CORE.Range range = new CORE.Range();
            range.decodeFromAttributes(decoder);
            decoder.rewindAttributes();
            while(true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if ((attribId == AttributeId.ATTRIB_READONLY) && decoder.readBool())
                    flags |= Varnode.varnode_flags.@readonly;
                else if ((attribId == AttributeId.ATTRIB_VOLATILE) && decoder.readBool())
                    flags |= Varnode.varnode_flags.volatil;
            }
            if (flags != 0) {
                glb.symboltab.setPropertyRange(flags, range);
            }
            decoder.closeElement(elemId);
        }

        /// \brief Parse a \<collision> element indicating a named symbol with no storage or
        /// data-type info
        /// Let the decompiler know that a name is occupied within the scope for isNameUsed
        /// queries, without specifying storage and data-type information about the symbol.
        /// This is modeled currently by creating an unmapped symbol.
        /// \param decoder is the stream decoder
        private void decodeCollision(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_COLLISION);
            string nm = decoder.readString(AttributeId.ATTRIB_NAME);
            decoder.closeElement(elemId);
            IEnumerator<Symbol>? iter = findFirstByName(nm);
            if (null == iter) {
                Datatype ct = glb.types.getBase(1, type_metatype.TYPE_INT);
                addSymbol(nm, ct);
            }
        }

        /// \brief Insert a Symbol into the \b nametree
        /// Duplicate symbol names are allowed for by establishing a deduplication id for the
        /// Symbol.
        /// \param sym is the Symbol to insert
        private void insertNameTree(Symbol sym)
        {
            sym.nameDedup = 0;
            Tuple<SymbolNameTree::iterator, bool> nameres;
            nameres = nametree.insert(sym);
            if (!nameres.second) {
                sym.nameDedup = 0xffffffff;
                SymbolNameTree::iterator iter = nametree.upper_bound(sym);
                --iter; // Last symbol with this name
                sym.nameDedup = iter.Current.nameDedup + 1;        // increment the dedup counter
                nameres = nametree.insert(sym);
                if (!nameres.second)
                    throw new LowlevelError("Could  not deduplicate symbol: " + sym.name);
            }
        }

        /// \brief Find an iterator pointing to the first Symbol in the ordering with a given name
        /// \param nm is the name to search for
        /// \return iterator pointing to the first Symbol or nametree.end() if there is no
        ///  matching Symbol
        private IEnumerator<Symbol>? findFirstByName(string nm)
        {
            Symbol sym = new Symbol((Scope)null, nm, (Datatype)null);
            IEnumerator<Symbol> iter = nametree.lower_bound(sym);
            if (iter == nametree.end()) return iter;
            if (iter.Current.getName() != nm)
                return nametree.end();
            return iter;
        }

        /// Build an unattached Scope to be associated as a sub-scope of \b this
        internal override Scope buildSubScope(ulong id, string nm)
        {
            return new ScopeInternal(id, nm, glb);
        }

        protected override void addSymbolInternal(Symbol sym)
        {
            if (sym.symbolId == 0) {
                sym.symbolId = Symbol.ID_BASE + ((uniqueId & 0xffff) << 40) + nextUniqueId;
                nextUniqueId += 1;
            }
            try {
                if (sym.name.Length == 0) {
                    sym.name = buildUndefinedName();
                    sym.displayName = sym.name;
                }
                if (sym.getType() == (Datatype)null)
                    throw new LowlevelError(sym.getName() + " symbol created with no type");
                if (sym.getType().getSize() < 1)
                    throw new LowlevelError(sym.getName() + " symbol created with zero size type");
                insertNameTree(sym);
                if (sym.category >= 0) {
                    while (category.Count <= (int)sym.category)
                        category.Add(new List<Symbol>());
                    List<Symbol> list = category[(int)sym.category];
                    if (sym.category > 0)
                        sym.catindex = (ushort)list.Count;
                    while (list.size() <= sym.catindex)
                        list.Add((Symbol)null);
                    list[sym.catindex] = sym;
                }
            }
            catch (LowlevelError err) {
                /// delete sym;         // Symbol must be deleted to avoid orphaning its memory
                throw err;
            }
        }

        protected override SymbolEntry addMapInternal(Symbol sym, Varnode.varnode_flags exfl, Address addr,
            int off, int sz, RangeList uselim)
        {
            // Find or create the appropriate rangemap
            AddrSpace spc = addr.getSpace();
            EntryMap? rangemap = maptable[spc.getIndex()];
            if (rangemap == (EntryMap)null) {
                rangemap = new EntryMap();
                maptable[spc.getIndex()] = rangemap;
            }
            // Insert the new map
            
            SymbolEntry.EntryInitData initdata =
                new SymbolEntry.EntryInitData(sym, exfl, addr.getSpace(), off, uselim);
            Address lastaddress = addr + (sz - 1);
            if (lastaddress.getOffset() < addr.getOffset()) {
                string msg = "Symbol ";
                msg += sym.getName();
                msg += " extends beyond the end of the address space";
                throw new LowlevelError(msg);
            }

            IEnumerator<SymbolEntry> iter = rangemap.insert(initdata, addr.getOffset(),
                lastaddress.getOffset());
            // Store reference to map in symbol
            sym.mapentry.Add(iter);
            if (sz == sym.type.getSize()) {
                sym.wholeCount += 1;
                if (sym.wholeCount == 2)
                    multiEntrySet.Add(sym);
            }
            return iter.Current;
        }

        protected override SymbolEntry addDynamicMapInternal(Symbol sym, Varnode.varnode_flags exfl, ulong hash,
            int off, int sz, RangeList uselim)
        {
            dynamicentry.Add(new SymbolEntry(sym, exfl, hash, off, sz, uselim));
            IEnumerator<SymbolEntry> iter = dynamicentry.end();
            --iter;
            // Store reference to map entry in symbol
            sym.mapentry.Add(iter);
            if (sz == sym.type.getSize()) {
                sym.wholeCount += 1;
                if (sym.wholeCount == 2)
                    multiEntrySet.Add(sym);
            }
            return dynamicentry.GetLastItem();
        }

        /// The set of Symbol objects, sorted by name
        protected SymbolNameTree nametree;
        /// Rangemaps of SymbolEntry, one map for each address space
        protected List<EntryMap> maptable = new List<EntryMap>();
        /// References to Symbol objects organized by category
        protected List<List<Symbol>> category = new List<List<Symbol>>();
        /// Dynamic symbol entries
        protected List<SymbolEntry> dynamicentry = new List<SymbolEntry>();
        /// Set of symbols with multiple entries
        protected SymbolNameTree multiEntrySet;
        /// Next available symbol id
        protected ulong nextUniqueId;

        /// Construct the Scope
        /// \param id is the globally unique id associated with the scope
        /// \param nm is the name of the Scope
        /// \param g is the Architecture it belongs to
        public ScopeInternal(ulong id, string nm, Architecture g)
            : base(id, nm, g, this)
        {
            nextUniqueId = 0;
            maptable.resize(g.numSpaces(), (EntryMap)null);
        }

        /// Construct as a cache
        public ScopeInternal(ulong id, string nm,Architecture g, Scope own)
            : base(id, nm, g, own)
        {
            nextUniqueId = 0;
            maptable.resize(g.numSpaces(), (EntryMap)null);
        }

        public override void clear()
        {
            SymbolNameTree::iterator iter = nametree.begin();
            while (iter.MoveNext()) {
                Symbol sym = *iter++;
                removeSymbol(sym);
            }
            nextUniqueId = 0;
        }

        /// Make sure Symbol categories are sane
        /// Look for NULL entries in the category tables. If there are,
        /// clear out the entire category, marking all symbols as uncategorized
        public virtual void categorySanity()
        {
            for (int i = 0; i < category.size(); ++i) {
                int num = category[i].size();
                if (num == 0) continue;
                bool nullsymbol = false;
                for (int j = 0; j < num; ++j) {
                    Symbol? sym = category[i][j];
                    if (sym == (Symbol)null) {
                        nullsymbol = true;  // There can be no null symbols
                        break;
                    }
                }
                if (nullsymbol) {
                    // Clear entire category
                    List<Symbol> list = new List<Symbol>();
                    for (int j = 0; j < num; ++j)
                        list.Add(category[i][j]);
                    for (int j = 0; j < list.size(); ++j) {
                        Symbol? sym = list[j];
                        if (sym == (Symbol)null) continue;
                        setCategory(sym, Symbol.SymbolCategory.no_category, 0);
                    }
                }
            }

        }

        public override void clearCategory(int cat)
        {
            if (cat >= 0) {
                if (cat >= category.size()) return; // Category doesn't exist
                int sz = category[cat].size();
                for (int i = 0; i < sz; ++i) {
                    Symbol sym = category[cat][i];
                    removeSymbol(sym);
                }
            }
            else {
                SymbolNameTree::iterator iter = nametree.begin();
                while (iter.moveNext()) {
                    Symbol sym = *iter++;
                    if (sym.getCategory() >= 0) continue;
                    removeSymbol(sym);
                }
            }
        }

        public override void clearUnlocked()
        {
            SymbolNameTree::iterator iter = nametree.begin();
            while (iter.moveNext()) {
                Symbol sym = *iter++;
                if (sym.isTypeLocked()) {
                    // Only hold if TYPE locked
                    if (!sym.isNameLocked()) {
                        // Clear an unlocked name
                        if (!sym.isNameUndefined()) {
                            renameSymbol(sym, buildUndefinedName());
                        }
                    }
                    clearAttribute(sym, Varnode.varnode_flags.nolocalalias); // Clear any calculated attributes
                    if (sym.isSizeTypeLocked())
                        resetSizeLockType(sym);
                }
                else if (sym.getCategory() == Symbol.SymbolCategory.equate) {
                    // Note we treat EquateSymbols as locked for purposes of this method
                    // as a typelock (which traditionally prevents a symbol from being cleared)
                    // does not make sense for an equate
                    continue;
                }
                else
                    removeSymbol(sym);
            }
        }

        public override void clearUnlockedCategory(int cat)
        {
            if (cat >= 0) {
                if (cat >= category.size()) return; // Category doesn't exist
                int sz = category[cat].size();
                for (int i = 0; i < sz; ++i) {
                    Symbol sym = category[cat][i];
                    if (sym.isTypeLocked()) {
                        // Only hold if TYPE locked
                        if (!sym.isNameLocked()) {
                            // Clear an unlocked name
                            if (!sym.isNameUndefined()) {
                                renameSymbol(sym, buildUndefinedName());
                            }
                        }
                        if (sym.isSizeTypeLocked())
                            resetSizeLockType(sym);
                    }
                    else
                        removeSymbol(sym);
                }
            }
            else {
                SymbolNameTree::iterator iter = nametree.begin();
                while (iter.moveNext()) {
                    Symbol sym = iter.Current;
                    if (sym.getCategory() >= 0) continue;
                    if (sym.isTypeLocked()) {
                        if (!sym.isNameLocked()) {
                            // Clear an unlocked name
                            if (!sym.isNameUndefined()) {
                                renameSymbol(sym, buildUndefinedName());
                            }
                        }
                    }
                    else
                        removeSymbol(sym);
                }
            }
        }

        public override void adjustCaches()
        {
            maptable.resize(glb.numSpaces(), delegate() { return null; });
        }

        ~ScopeInternal()
        {
            //List<EntryMap*>::iterator iter1;

            //for (iter1 = maptable.begin(); iter1 != maptable.end(); ++iter1)
            //    if ((*iter1) != (EntryMap)null)
            //        delete* iter1;

            //SymbolNameTree::iterator iter2;

            //for (iter2 = nametree.begin(); iter2 != nametree.end(); ++iter2)
            //    delete* iter2;
        }

        public override MapIterator begin()
        {
            // The symbols are ordered via their mapping address
            IEnumerator<EntryMap> iter = maptable.begin();
            while (iter.MoveNext() && (iter.Current == (EntryMap)null)) { }
            IEnumerator<SymbolEntry> curiter;
            if (iter != maptable.end()) {
                curiter = iter.Current.begin_list();
                if (curiter == iter.Current.end_list()) {
                    while ((iter != maptable.end()) && (curiter == iter.Current.end_list())) {
                        do {
                            ++iter;
                        } while ((iter != maptable.end()) && (iter.Current == (EntryMap)null));
                        if (iter != maptable.end())
                            curiter = iter.Current.begin_list();
                    }

                }
            }
            return new MapIterator(maptable, iter, curiter);
        }

        //public override MapIterator end()
        //{
        //    IEnumerator<SymbolEntry> curiter;
        //    return new MapIterator(&maptable, maptable.end(), curiter);
        //}

        public override IEnumerator<SymbolEntry> beginDynamic() => dynamicentry.GetEnumerator();

        //public override IEnumerator<SymbolEntry> endDynamic() => dynamicentry.end();

        // public override IEnumerator<SymbolEntry> beginDynamic() => dynamicentry.begin();

        //public override IEnumerator<SymbolEntry> endDynamic() => dynamicentry.end();

        public override void removeSymbolMappings(Symbol symbol)
        {
            if (symbol.wholeCount > 1)
                multiEntrySet.Remove(symbol);
            // Remove each mapping of the symbol
            IEnumerator<IEnumerator<SymbolEntry>> iter = symbol.mapentry.GetEnumerator();
            while (iter.MoveNext()) {
                AddrSpace? spc = iter.Current.Current.getAddr().getSpace();
                if (spc == (AddrSpace)null)
                    // A null address indicates a dynamic mapping
                    dynamicentry.Remove(iter.Current.Current);
                else {
                    EntryMap rangemap = maptable[spc.getIndex()];
                    rangemap.erase(iter.Current);
                }
            }
            symbol.wholeCount = 0;
            symbol.mapentry.Clear();
        }

        public override void removeSymbol(Symbol symbol)
        {
            if (symbol.category >= 0) {
                List<Symbol> list = category[symbol.category];
                list[symbol.catindex] = (Symbol)null;
                while ((!list.empty()) && (list.GetLastItem() == (Symbol)null))
                    list.RemoveLastItem();
            }
            removeSymbolMappings(symbol);
            nametree.erase(symbol);
            // delete symbol;
        }

        public override void renameSymbol(Symbol sym, string newname)
        {
            nametree.Remove(sym);        // Erase under old name
            if (sym.wholeCount > 1)
                multiEntrySet.Remove(sym);   // The multi-entry set is sorted by name, remove
            string oldname = sym.name;
            sym.name = newname;
            sym.displayName = newname;
            insertNameTree(sym);
            if (sym.wholeCount > 1)
                multiEntrySet.Add(sym);  // Reenter into the multi-entry set now that name is changed
        }

        public override void retypeSymbol(Symbol sym, Datatype ct)
        {
            if (ct.hasStripped())
                ct = ct.getStripped();
            if ((sym.type.getSize() == ct.getSize()) || (sym.mapentry.empty())) {
                // If size is the same, or no mappings nothing special to do
                sym.type = ct;
                sym.checkSizeTypeLock();
                return;
            }
            else if (sym.mapentry.size() == 1) {
                IEnumerator<SymbolEntry> iter = sym.mapentry.GetLastItem();
                if (iter.Current.isAddrTied()) {
                    // Save the starting address of map
                    Address addr = iter.Current.getAddr();

                    // Find the correct rangemap
                    EntryMap rangemap = maptable[iter.Current.getAddr().getSpace().getIndex()];
                    // Remove the map entry
                    rangemap.erase(iter);
                    sym.mapentry.RemoveLastItem();   // Remove reference to map entry
                    sym.wholeCount = 0;

                    // Now we are ready to change the type
                    sym.type = ct;
                    sym.checkSizeTypeLock();
                    addMapPoint(sym, addr, new Address()); // Re-add map with new size
                    return;
                }
            }
            throw new RecovError("Unable to retype symbol: " + sym.name);
        }

        public override void setAttribute(Symbol sym, Varnode.varnode_flags attr)
        {
            attr &= (Varnode.varnode_flags.typelock | Varnode.varnode_flags.namelock |
                Varnode.varnode_flags.@readonly | Varnode.varnode_flags.incidental_copy |
                Varnode.varnode_flags.nolocalalias | Varnode.varnode_flags.volatil |
                Varnode.varnode_flags.indirectstorage | Varnode.varnode_flags.hiddenretparm);
            sym.flags |= attr;
            sym.checkSizeTypeLock();
        }

        public override void clearAttribute(Symbol sym, Varnode.varnode_flags attr)
        {
            attr &= (Varnode.varnode_flags.typelock | Varnode.varnode_flags.namelock |
                Varnode.varnode_flags.@readonly | Varnode.varnode_flags.incidental_copy |
                Varnode.varnode_flags.nolocalalias | Varnode.varnode_flags.volatil |
                Varnode.varnode_flags.indirectstorage | Varnode.varnode_flags.hiddenretparm);
            sym.flags &= ~attr;
            sym.checkSizeTypeLock();
        }

        public override void setDisplayFormat(Symbol sym, Symbol.DisplayFlags attr)
        {
            sym.setDisplayFormat(attr);
        }

        public override SymbolEntry findAddr(Address addr, Address usepoint)
        {
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                Tuple<EntryMap::const_iterator, EntryMap::const_iterator> res;
                if (usepoint.isInvalid())
                    res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                        EntryMap::subsorttype(true));
                else
                    res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                        EntryMap::subsorttype(usepoint));
                while (res.first != res.second) {
                    --res.second;
                    SymbolEntry entry = &(*res.second);
                    if (entry.getAddr().getOffset() == addr.getOffset()) {
                        if (entry.inUse(usepoint))
                            return entry;
                    }
                }
            }
            return (SymbolEntry)null;
        }

        public override SymbolEntry? findContainer(Address addr,int size, Address usepoint)
        {
            SymbolEntry? bestentry = (SymbolEntry)null;
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                Tuple<EntryMap::const_iterator, EntryMap::const_iterator> res;
                if (usepoint.isInvalid())
                    res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                        EntryMap::subsorttype(true));
                else
                    res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                        EntryMap::subsorttype(usepoint));
                int oldsize = -1;
                ulong end = addr.getOffset() + (uint)size - 1;
                while (res.Item1 != res.Item2) {
                    --res.second;
                    SymbolEntry entry = res.Item2;
                    if (entry.getLast() >= end) {
                        // We contain the range
                        if ((entry.getSize() < oldsize) || (oldsize == -1)) {
                            if (entry.inUse(usepoint)) {
                                bestentry = entry;
                                if (entry.getSize() == size) break;
                                oldsize = entry.getSize();
                            }
                        }
                    }
                }
            }
            return bestentry;
        }

        public override SymbolEntry? findClosestFit(Address addr,int size, Address usepoint)
        {
            SymbolEntry? bestentry = (SymbolEntry)null;
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                Tuple<EntryMap::const_iterator, EntryMap::const_iterator> res;
                if (usepoint.isInvalid())
                    res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                        EntryMap::subsorttype(true));
                else
                    res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                        EntryMap::subsorttype(usepoint));
                int olddiff = -10000;
                int newdiff;

                while (res.first != res.second) {
                    --res.second;
                    SymbolEntry entry = res.second;
                    if (entry.getLast() >= addr.getOffset()) {
                        // We contain start
                        newdiff = entry.getSize() - size;
                        if (((olddiff < 0) && (newdiff > olddiff)) ||
                            ((olddiff >= 0) && (newdiff >= 0) && (newdiff < olddiff)))
                        {
                            if (entry.inUse(usepoint)) {
                                bestentry = entry;
                                if (newdiff == 0) break;
                                olddiff = newdiff;
                            }
                        }
                    }
                }
            }
            return bestentry;
        }

        public override Funcdata? findFunction(Address addr)
        {
            FunctionSymbol sym;
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                Tuple<EntryMap::const_iterator, EntryMap::const_iterator> res;
                res = rangemap.find(addr.getOffset());
                while (res.first != res.second) {
                    SymbolEntry entry = &(*res.first);
                    if (entry.getAddr().getOffset() == addr.getOffset()) {
                        sym = (FunctionSymbol)(entry.getSymbol());
                        if (sym != (FunctionSymbol)null)
                            return sym.getFunction();
                    }
                    ++res.first;
                }
            }
            return (Funcdata)null;
        }

        public override ExternRefSymbol? findExternalRef(Address addr)
        {
            ExternRefSymbol? sym = (ExternRefSymbol)null;
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                Tuple<EntryMap::const_iterator, EntryMap::const_iterator> res;
                res = rangemap.find(addr.getOffset());
                while (res.first != res.second) {
                    SymbolEntry entry = &(*res.first);
                    if (entry.getAddr().getOffset() == addr.getOffset()) {
                        sym = entry.getSymbol() as ExternRefSymbol;
                        break;
                    }
                    ++res.first;
                }
            }
            return sym;
        }

        public override LabSymbol? findCodeLabel(Address addr)
        {
            LabSymbol? sym = (LabSymbol)null;
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                Tuple<EntryMap::const_iterator, EntryMap::const_iterator> res;
                res = rangemap.find(addr.getOffset(), EntryMap::subsorttype(false),
                    EntryMap::subsorttype(addr));
                while (res.first != res.second) {
                    --res.second;
                    SymbolEntry entry = &(*res.second);
                    if (entry.getAddr().getOffset() == addr.getOffset()) {
                        if (entry.inUse(addr)) {
                            sym = entry.getSymbol() as LabSymbol;
                            break;
                        }
                    }
                }
            }
            return sym;
        }

        public override SymbolEntry findOverlap(Address addr,int size)
        {
            EntryMap? rangemap = maptable[addr.getSpace().getIndex()];
            if (rangemap != (EntryMap)null) {
                EntryMap::const_iterator iter;
                iter = rangemap.find_overlap(addr.getOffset(), addr.getOffset() + (uint)size - 1);
                if (iter != rangemap.end())
                    return &(*iter);
            }
            return (SymbolEntry)null;
        }

        public override void findByName(string nm, List<Symbol> res)
        {
            IEnumerator<Symbol>? iter = findFirstByName(nm);
            if (null == iter) return;
            do {
                Symbol sym = iter.Current;
                if (sym.name != nm) break;
                res.Add(sym);
            } while (iter.MoveNext());
        }

        public override bool isNameUsed(string nm, Scope? op2)
        {
            Symbol sym = new Symbol((Scope)null,nm,(Datatype)null);
            IEnumerator<Symbol> iter = nametree.lower_bound(sym);
            if (iter != nametree.end()) {
                if ((*iter).getName() == nm)
                    return true;
            }
            Scope? par = getParent();
            if (par == (Scope)null || par == op2)
                return false;
            if (par.getParent() == (Scope)null)  // Never recurse into global scope
                return false;
            return par.isNameUsed(nm, op2);
        }

        public override Funcdata? resolveExternalRefFunction(ExternRefSymbol sym)
        {
            return queryFunction(sym.getRefAddr());
        }

        public override string buildVariableName(Address addr, Address pc, Datatype? ct, int index,
            Varnode.varnode_flags flags)
        {
            TextWriter s = new StringWriter();
            int sz = (ct == (Datatype)null) ? 1 : ct.getSize();

            if ((flags & Varnode.varnode_flags.unaffected) != 0) {
                if ((flags & Varnode.varnode_flags.return_address) != 0)
                    s.Write("unaff_retaddr");
                else {
                    string unaffname;
                    unaffname = glb.translate.getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                    if (unaffname.empty()) {
                        s.Write($"unaff_{addr.getOffset():X08}");
                    }
                    else
                        s.Write($"unaff_{unaffname}");
                }
            }
            else if ((flags & Varnode.varnode_flags.persist) != 0) {
                string spacename;
                spacename = glb.translate.getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                if (!spacename.empty())
                    s.Write(spacename);
                else {
                    if (ct != (Datatype)null)
                        ct.printNameBase(s);
                    spacename = addr.getSpace().getName();
                    spacename = spacename.Capitalize(); // Capitalize space
                    s.Write(spacename);
                    string formatString = $"{{0:0X{2 * addr.getAddrSize()}}}";
                    s.Write(string.Format(formatString,
                        AddrSpace.byteToAddress(addr.getOffset(), addr.getSpace().getWordSize())));
                }
            }
            else if (((flags & Varnode.varnode_flags.input) != 0) && (index < 0)) {
                // Irregular input
                string regname;
                regname = glb.translate.getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                if (regname.empty()) {
                    s.Write($"in_{addr.getSpace().getName()}_");
                    s.Write($"{addr.getOffset():X08}");
                }
                else
                    s.Write($"in_{regname}");
            }
            else if ((flags & Varnode.varnode_flags.input) != 0) {
                // Regular parameter
                s.Write($"param_{index}");
            }
            else if ((flags & Varnode.varnode_flags.addrtied) != 0) {
                if (ct != (Datatype)null)
                    ct.printNameBase(s);
                string spacename = addr.getSpace().getName();
                spacename = spacename.Capitalize(); // Capitalize space
                s.Write(spacename);
                string formatString = $"0:X0{2 * addr.getAddrSize()}";
                s.Write(formatString, AddrSpace.byteToAddress(addr.getOffset(), addr.getSpace().getWordSize()));
            }
            else if ((flags & Varnode.varnode_flags.indirect_creation) != 0) {
                string regname;
                s.Write("extraout_");
                regname = glb.translate.getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                if (!regname.empty())
                    s.Write(regname);
                else
                    s.Write("var");
            }
            else {
                // Some sort of local variable
                if (ct != (Datatype)null)
                    ct.printNameBase(s);
                s.Write($"Var{index++}");
                if (null != findFirstByName(s.ToString())) {
                    // If the name already exists
                    for (int i = 0; i < 10; ++i) {
                        // Try bumping up the index a few times before calling makeNameUnique
                        TextWriter s2 = new StringWriter();
                        if (ct != (Datatype)null)
                            ct.printNameBase(s2);
                        s2.Write($"Var{index++}");
                        if (null == findFirstByName(s2.ToString())) {
                            return s2.ToString();
                        }
                    }
                }
            }
            return makeNameUnique(s.ToString());
        }

        public override string buildUndefinedName()
        {
            // We maintain a family of officially undefined names
            // so that symbols can be stored in the database without
            // having their name defined
            // We generate a name of the form '$$undefXXXXXXXX'
            // The dollar signs indicate a special name (not a legal identifier)
            // undef indicates an undefined name and the remaining
            // characters are hex digits which make the name unique
            IEnumerator<Symbol> iter;

            Symbol testsym = new Symbol((Scope)null, "$$undefz", (Datatype)null);

            iter = nametree.lower_bound(testsym);
            if (iter != nametree.begin())
                --iter;
            if (iter != nametree.end()) {
                string symname = iter.Current.getName();
                if ((symname.Length == 15) && symname.StartsWith("$$undef")) {
                    uint uniq = uint.MaxValue;
                    uint.TryParse(symname.Substring(7, 8), NumberStyles.HexNumber, null, out uniq);
                    if (uniq == uint.MaxValue)
                        throw new LowlevelError("Error creating undefined name");
                    uniq += 1;
                    return $"$$undef{uniq:X08}";
                }
            }
            return "$$undef00000000";
        }

        public override string makeNameUnique(string nm)
        {
            IEnumerator<Symbol>? iter = findFirstByName(nm);
            if (null == iter) return nm; // nm is already unique

            Symbol boundsym = new Symbol((Scope)null,nm + "_x99999",(Datatype)null);
            boundsym.nameDedup = 0xffffffff;
            IEnumerator<Symbol> iter2 = nametree.lower_bound(boundsym);
            uint uniqid;
            do {
                uniqid = 0xffffffff;
                --iter2;            // Last symbol whose name starts with nm
                if (iter == iter2) break;
                Symbol bsym = iter2.Current;
                string bname = bsym.getName();
                bool isXForm = false;
                int digCount = 0;
                if ((bname.Length >= (nm.Length + 3)) && (bname[nm.Length] == '_')) {
                    // Collect the last id
                    int i = nm.Length + 1;
                    if (bname[i] == 'x') {
                        i += 1;         // 5 digit form
                        isXForm = true;
                    }
                    uniqid = 0;
                    for (; i < bname.Length; ++i) {
                        char dig = bname[i];
                        if (!char.IsDigit(dig)) {
                            // Everything after '_' must be a digit, or not in our format
                            uniqid = 0xffffffff;
                            break;
                        }
                        uniqid *= 10;
                        uniqid += (uint)(dig - '0');
                        digCount += 1;
                    }
                }
                if (isXForm && (digCount != 5)) // x form, but not right number of digits
                    uniqid = 0xffffffff;
                else if ((!isXForm) && (digCount != 2))
                    uniqid = 0xffffffff;
            } while (uniqid == 0xffffffff);

            string resString;
            if (uniqid == 0xffffffff) {
                // no other names matching our convention
                resString = nm + "_00";     // Start a new sequence
            }
            else {
                uniqid += 1;
                TextWriter s = new StringWriter();
                s.Write($"{nm}_"); // << dec << setfill('0');
                if (uniqid < 100)
                    s.Write($"{uniqid:02}");
                else
                    s.Write($"x{uniqid:05}");
                resString = s.ToString();
            }
            if (null != findFirstByName(resString))
                throw new LowlevelError($"Unable to uniquify name: {resString}");
            return resString;
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_SCOPE);
            encoder.writeString(AttributeId.ATTRIB_NAME, name);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_ID, uniqueId);
            if (getParent() != (Scope)null) {
                encoder.openElement(ElementId.ELEM_PARENT);
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_ID, getParent().getId());
                encoder.closeElement(ElementId.ELEM_PARENT);
            }
            getRangeTree().encode(encoder);

            if (!nametree.empty()) {
                encoder.openElement(ElementId.ELEM_SYMBOLLIST);
                IEnumerator<Symbol> iter;
                for (iter = nametree.begin(); iter != nametree.end(); ++iter) {
                    Symbol sym = iter.Current;
                    int symbolType = 0;
                    if (!sym.mapentry.empty()) {
                        SymbolEntry entry = sym.mapentry.front();
                        if (entry.isDynamic()) {
                            if (sym.getCategory() == Symbol.SymbolCategory.union_facet)
                                continue;       // Don't save override
                            symbolType = (sym.getCategory() == Symbol.SymbolCategory.equate) ? 2 : 1;
                        }
                    }
                    encoder.openElement(ElementId.ELEM_MAPSYM);
                    if (symbolType == 1)
                        encoder.writeString(AttributeId.ATTRIB_TYPE, "dynamic");
                    else if (symbolType == 2)
                        encoder.writeString(AttributeId.ATTRIB_TYPE, "equate");
                    sym.encode(encoder);
                    List<IEnumerator<SymbolEntry>> miter;
                    for (miter = sym.mapentry.begin(); miter != sym.mapentry.end(); ++miter) {
                        SymbolEntry entry = (*(*miter));
                        entry.encode(encoder);
                    }
                    encoder.closeElement(ElementId.ELEM_MAPSYM);
                }
                encoder.closeElement(ElementId.ELEM_SYMBOLLIST);
            }
            encoder.closeElement(ElementId.ELEM_SCOPE);
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            //  uint elemId = decoder.openElement(ElementId.ELEM_SCOPE);
            //  name = el.getAttributeValue("name");	// Name must already be set in the constructor
            bool rangeequalssymbols = false;

            ElementId subId = decoder.peekElement();
            if (subId == ElementId.ELEM_PARENT) {
                decoder.skipElement();  // Skip <parent> tag processed elsewhere
                subId = decoder.peekElement();
            }
            if (subId == ElementId.ELEM_RANGELIST) {
                RangeList newrangetree = new RangeList();
                newrangetree.decode(decoder);
                glb.symboltab.setRange(this, newrangetree);
            }
            else if (subId == ElementId.ELEM_RANGEEQUALSSYMBOLS) {
                decoder.openElement();
                decoder.closeElement(subId);
                rangeequalssymbols = true;
            }
            subId = decoder.openElement(ElementId.ELEM_SYMBOLLIST);
            if (subId != 0) {
                while(true) {
                    ElementId symId = decoder.peekElement();
                    if (symId == 0) break;
                    if (symId == ElementId.ELEM_MAPSYM) {
                        Symbol sym = addMapSym(decoder) ?? throw new ApplicationException();
                        if (rangeequalssymbols) {
                            SymbolEntry e = sym.getFirstWholeMap();
                            glb.symboltab.addRange(this, e.getAddr().getSpace(), e.getFirst(), e.getLast());
                        }
                    }
                    else if (symId == ElementId.ELEM_HOLE)
                        decodeHole(decoder);
                    else if (symId == ElementId.ELEM_COLLISION)
                        decodeCollision(decoder);
                    else
                        throw new LowlevelError("Unknown symbollist tag");
                }
                decoder.closeElement(subId);
            }
            //  decoder.closeElement(elemId);
            categorySanity();
        }

        public override void printEntries(TextWriter s)
        {
            s.WriteLine($"Scope {name}");
            for (int i = 0; i < maptable.Count; ++i) {
                EntryMap? rangemap = maptable[i];
                if (rangemap == (EntryMap)null) continue;
                IEnumerator<SymbolEntry> iter = rangemap.begin_list();
                while (iter.MoveNext())
                    iter.Current.printEntry(s);
            }
        }

        public override int getCategorySize(Symbol.SymbolCategory cat)
        {
            if (((int)cat >= category.size()) || ((int)cat < 0))
                return 0;
            return category[(int)cat].size();
        }

        public override Symbol? getCategorySymbol(Symbol.SymbolCategory cat, int ind)
        {
            if (((int)cat >= category.Count) || (cat < 0))
                return (Symbol)null;
            if ((ind < 0) || (ind >= category[(int)cat].size()))
                return (Symbol)null;
            return category[(int)cat][ind];
        }

        public override void setCategory(Symbol sym, Symbol.SymbolCategory cat, int ind)
        {
            List<Symbol?> list;

            if (sym.category >= 0) {
                list = category[(int)sym.category];
                list[sym.catindex] = (Symbol)null;
                while ((!list.empty()) && (list.GetLastItem() == (Symbol)null))
                    list.RemoveLastItem();
            }

            sym.category = cat;
            sym.catindex = (ushort)ind;
            if (cat < 0) return;
            while (category.size() <= (int)sym.category)
                category.Add(new List<Symbol>());
            list = category[(int)sym.category];
            while (list.size() <= sym.catindex)
                list.Add((Symbol)null);
            list[sym.catindex] = sym;
        }

        /// Assign a default name (via buildVariableName) to any unnamed symbol
        /// Run through all the symbols whose name is undefined. Build a variable name, uniquify it, and
        /// rename the variable.
        /// \param base is the base index to start at for generating generic names
        public override void assignDefaultNames(int @base)
        {
            IEnumerator<Symbol> iter;

            Symbol testsym = new Symbol((Scope)null, "$$undef", (Datatype)null);

            iter = nametree.upper_bound(testsym);
            while (iter != nametree.end()) {
                Symbol sym = *iter;
                if (!sym.isNameUndefined()) break;
                ++iter;     // Advance before renaming
                string nm = buildDefaultName(sym, @base, (Varnode)null);
                renameSymbol(sym, nm);
            }
        }

        /// Start of symbols with more than one entry
        public IEnumerator<Symbol> beginMultiEntry() => multiEntrySet.GetEnumerator();

        ///// End of symbols with more than one entry
        //public IEnumerator<Symbol> endMultiEntry() => multiEntrySet.end();
    }
}
