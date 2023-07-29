using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

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
        private void decodeHole(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_HOLE);
            uint flags = 0;
            Range range;
            range.decodeFromAttributes(decoder);
            decoder.rewindAttributes();
            for (; ; ) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if ((attribId == AttributeId.ATTRIB_READONLY) && decoder.readBool())
                    flags |= Varnode::@readonly;
                else if ((attribId == AttributeId.ATTRIB_VOLATILE) && decoder.readBool())
                    flags |= Varnode::volatil;
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
        private void decodeCollision(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_COLLISION);
            string nm = decoder.readString(AttributeId.ATTRIB_NAME);
            decoder.closeElement(elemId);
            SymbolNameTree::const_iterator iter = findFirstByName(nm);
            if (iter == nametree.end()) {
                Datatype ct = glb.types.getBase(1, TYPE_INT);
                addSymbol(nm, ct);
            }
        }

        /// \brief Insert a Symbol into the \b nametree
        /// Duplicate symbol names are allowed for by establishing a deduplication id for the
        /// Symbol.
        /// \param sym is the Symbol to insert
        private void insertNameTree(Symbol sym)
        {
            sym->nameDedup = 0;
            pair<SymbolNameTree::iterator, bool> nameres;
            nameres = nametree.insert(sym);
            if (!nameres.second)
            {
                sym->nameDedup = 0xffffffff;
                SymbolNameTree::iterator iter = nametree.upper_bound(sym);
                --iter; // Last symbol with this name
                sym->nameDedup = (*iter)->nameDedup + 1;        // increment the dedup counter
                nameres = nametree.insert(sym);
                if (!nameres.second)
                    throw new LowlevelError("Could  not deduplicate symbol: " + sym->name);
            }
        }

        /// \brief Find an iterator pointing to the first Symbol in the ordering with a given name
        /// \param nm is the name to search for
        /// \return iterator pointing to the first Symbol or nametree.end() if there is no matching Symbol
        private SymbolNameTree::const_iterator findFirstByName(string nm)
        {
            Symbol sym((Scope*)0,nm,(Datatype*)0);
            SymbolNameTree::const_iterator iter = nametree.lower_bound(&sym);
            if (iter == nametree.end()) return iter;
            if ((*iter)->getName() != nm)
                return nametree.end();
            return iter;
        }

        /// Build an unattached Scope to be associated as a sub-scope of \b this
        protected override Scope buildSubScope(ulong id, string nm)
        {
            return new ScopeInternal(id, nm, glb);
        }

        protected override void addSymbolInternal(Symbol sym)
        {
            if (sym->symbolId == 0)
            {
                sym->symbolId = Symbol::ID_BASE + ((uniqueId & 0xffff) << 40) + nextUniqueId;
                nextUniqueId += 1;
            }
            try
            {
                if (sym->name.size() == 0)
                {
                    sym->name = buildUndefinedName();
                    sym->displayName = sym->name;
                }
                if (sym->getType() == (Datatype*)0)
                    throw new LowlevelError(sym->getName() + " symbol created with no type");
                if (sym->getType()->getSize() < 1)
                    throw new LowlevelError(sym->getName() + " symbol created with zero size type");
                insertNameTree(sym);
                if (sym->category >= 0)
                {
                    while (category.size() <= sym->category)
                        category.push_back(vector<Symbol*>());
                    vector<Symbol*> & list(category[sym->category]);
                    if (sym->category > 0)
                        sym->catindex = list.size();
                    while (list.size() <= sym->catindex)
                        list.push_back((Symbol*)0);
                    list[sym->catindex] = sym;
                }
            }
            catch (LowlevelError err) {
                delete sym;         // Symbol must be deleted to avoid orphaning its memory
                throw err;
            }
        }

        protected override SymbolEntry addMapInternal(Symbol sym, uint4 exfl, Address addr,
            int4 off, int4 sz, RangeList uselim)
        {
            // Find or create the appropriate rangemap
            AddrSpace* spc = addr.getSpace();
            EntryMap* rangemap = maptable[spc->getIndex()];
            if (rangemap == (EntryMap*)0)
            {
                rangemap = new EntryMap();
                maptable[spc->getIndex()] = rangemap;
            }
            // Insert the new map
            SymbolEntry::inittype initdata(sym, exfl, addr.getSpace(), off, uselim);
            Address lastaddress = addr + (sz - 1);
            if (lastaddress.getOffset() < addr.getOffset())
            {
                string msg = "Symbol ";
                msg += sym->getName();
                msg += " extends beyond the end of the address space";
                throw new LowlevelError(msg);
            }

            list<SymbolEntry>::iterator iter = rangemap->insert(initdata, addr.getOffset(), lastaddress.getOffset());
            // Store reference to map in symbol
            sym->mapentry.push_back(iter);
            if (sz == sym->type->getSize())
            {
                sym->wholeCount += 1;
                if (sym->wholeCount == 2)
                    multiEntrySet.insert(sym);
            }
            return &(*iter);
        }

        protected override SymbolEntry addDynamicMapInternal(Symbol sym, uint4 exfl, uint8 hash,
            int4 off, int4 sz, RangeList uselim)
        {
            dynamicentry.push_back(SymbolEntry(sym, exfl, hash, off, sz, uselim));
            list<SymbolEntry>::iterator iter = dynamicentry.end();
            --iter;
            sym->mapentry.push_back(iter); // Store reference to map entry in symbol
            if (sz == sym->type->getSize())
            {
                sym->wholeCount += 1;
                if (sym->wholeCount == 2)
                    multiEntrySet.insert(sym);
            }
            return &dynamicentry.back();
        }

        /// The set of Symbol objects, sorted by name
        protected SymbolNameTree nametree;
        /// Rangemaps of SymbolEntry, one map for each address space
        protected List<EntryMap> maptable;
        /// References to Symbol objects organized by category
        protected List<List<Symbol>> category;
        /// Dynamic symbol entries
        protected List<SymbolEntry> dynamicentry;
        /// Set of symbols with multiple entries
        protected SymbolNameTree multiEntrySet;
        /// Next available symbol id
        protected uint8 nextUniqueId;

        /// Construct the Scope
        /// \param id is the globally unique id associated with the scope
        /// \param nm is the name of the Scope
        /// \param g is the Architecture it belongs to
        public ScopeInternal(uint8 id, string nm, Architecture g)
            : base(id, nm, g, this)
        {
            nextUniqueId = 0;
            maptable.resize(g->numSpaces(), (EntryMap*)0);
        }

        /// Construct as a cache
        public ScopeInternal(uint8 id, string nm,Architecture g, Scope own)
            : base(id, nm, g, own)
        {
            nextUniqueId = 0;
            maptable.resize(g->numSpaces(), (EntryMap*)0);
        }

        public override void clear()
        {
            SymbolNameTree::iterator iter;

            iter = nametree.begin();
            while (iter != nametree.end())
            {
                Symbol* sym = *iter++;
                removeSymbol(sym);
            }
            nextUniqueId = 0;
        }

        /// Make sure Symbol categories are sane
        /// Look for NULL entries in the category tables. If there are,
        /// clear out the entire category, marking all symbols as uncategorized
        public override void categorySanity()
        {
            for (int4 i = 0; i < category.size(); ++i)
            {
                int4 num = category[i].size();
                if (num == 0) continue;
                bool nullsymbol = false;
                for (int4 j = 0; j < num; ++j)
                {
                    Symbol* sym = category[i][j];
                    if (sym == (Symbol*)0)
                    {
                        nullsymbol = true;  // There can be no null symbols
                        break;
                    }
                }
                if (nullsymbol)
                {       // Clear entire category
                    vector<Symbol*> list;
                    for (int4 j = 0; j < num; ++j)
                        list.push_back(category[i][j]);
                    for (int4 j = 0; j < list.size(); ++j)
                    {
                        Symbol* sym = list[j];
                        if (sym == (Symbol*)0) continue;
                        setCategory(sym, Symbol::no_category, 0);
                    }
                }
            }

        }

        public override void clearCategory(int4 cat)
        {
            if (cat >= 0)
            {
                if (cat >= category.size()) return; // Category doesn't exist
                int4 sz = category[cat].size();
                for (int4 i = 0; i < sz; ++i)
                {
                    Symbol* sym = category[cat][i];
                    removeSymbol(sym);
                }
            }
            else
            {
                SymbolNameTree::iterator iter;
                iter = nametree.begin();
                while (iter != nametree.end())
                {
                    Symbol* sym = *iter++;
                    if (sym->getCategory() >= 0) continue;
                    removeSymbol(sym);
                }
            }
        }

        public override void clearUnlocked()
        {
            SymbolNameTree::iterator iter;

            iter = nametree.begin();
            while (iter != nametree.end())
            {
                Symbol* sym = *iter++;
                if (sym->isTypeLocked())
                {   // Only hold if TYPE locked
                    if (!sym->isNameLocked())
                    { // Clear an unlocked name
                        if (!sym->isNameUndefined())
                        {
                            renameSymbol(sym, buildUndefinedName());
                        }
                    }
                    clearAttribute(sym, Varnode::nolocalalias); // Clear any calculated attributes
                    if (sym->isSizeTypeLocked())
                        resetSizeLockType(sym);
                }
                else if (sym->getCategory() == Symbol::equate)
                {
                    // Note we treat EquateSymbols as locked for purposes of this method
                    // as a typelock (which traditionally prevents a symbol from being cleared)
                    // does not make sense for an equate
                    continue;
                }
                else
                    removeSymbol(sym);
            }
        }

        public override void clearUnlockedCategory(int4 cat)
        {
            if (cat >= 0)
            {
                if (cat >= category.size()) return; // Category doesn't exist
                int4 sz = category[cat].size();
                for (int4 i = 0; i < sz; ++i)
                {
                    Symbol* sym = category[cat][i];
                    if (sym->isTypeLocked())
                    { // Only hold if TYPE locked
                        if (!sym->isNameLocked())
                        { // Clear an unlocked name
                            if (!sym->isNameUndefined())
                            {
                                renameSymbol(sym, buildUndefinedName());
                            }
                        }
                        if (sym->isSizeTypeLocked())
                            resetSizeLockType(sym);
                    }
                    else
                        removeSymbol(sym);
                }
            }
            else
            {
                SymbolNameTree::iterator iter;
                iter = nametree.begin();
                while (iter != nametree.end())
                {
                    Symbol* sym = *iter++;
                    if (sym->getCategory() >= 0) continue;
                    if (sym->isTypeLocked())
                    {
                        if (!sym->isNameLocked())
                        { // Clear an unlocked name
                            if (!sym->isNameUndefined())
                            {
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
            maptable.resize(glb->numSpaces(), (EntryMap*)0);
        }

        ~ScopeInternal()
        {
            vector<EntryMap*>::iterator iter1;

            for (iter1 = maptable.begin(); iter1 != maptable.end(); ++iter1)
                if ((*iter1) != (EntryMap*)0)
                    delete* iter1;

            SymbolNameTree::iterator iter2;

            for (iter2 = nametree.begin(); iter2 != nametree.end(); ++iter2)
                delete* iter2;
        }

        public override MapIterator begin()
        {
            // The symbols are ordered via their mapping address
            vector<EntryMap*>::const_iterator iter;
            iter = maptable.begin();
            while ((iter != maptable.end()) && ((*iter) == (EntryMap*)0))
                ++iter;
            list<SymbolEntry>::const_iterator curiter;
            if (iter != maptable.end())
            {
                curiter = (*iter)->begin_list();
                if (curiter == (*iter)->end_list())
                {
                    while ((iter != maptable.end()) && (curiter == (*iter)->end_list()))
                    {
                        do
                        {
                            ++iter;
                        } while ((iter != maptable.end()) && ((*iter) == (EntryMap*)0));
                        if (iter != maptable.end())
                            curiter = (*iter)->begin_list();
                    }

                }
            }
            return MapIterator(&maptable, iter, curiter);
        }

        public override MapIterator end()
        {
            list<SymbolEntry>::const_iterator curiter;
            return MapIterator(&maptable, maptable.end(), curiter);
        }

        public override IEnumerator<SymbolEntry> beginDynamic()
        {
            return dynamicentry.begin();
        }

        public override IEnumerator<SymbolEntry> endDynamic()
        {
            return dynamicentry.end();
        }

        public override IEnumerator<SymbolEntry> beginDynamic()
        {
            return dynamicentry.begin();
        }

        public override IEnumerator<SymbolEntry> endDynamic()
        {
            return dynamicentry.end();
        }

        public override void removeSymbolMappings(Symbol symbol)
        {
            vector<list<SymbolEntry>::iterator>::iterator iter;

            if (symbol->wholeCount > 1)
                multiEntrySet.erase(symbol);
            // Remove each mapping of the symbol
            for (iter = symbol->mapentry.begin(); iter != symbol->mapentry.end(); ++iter)
            {
                AddrSpace* spc = (*(*iter)).getAddr().getSpace();
                if (spc == (AddrSpace*)0) // A null address indicates a dynamic mapping
                    dynamicentry.erase(*iter);
                else
                {
                    EntryMap* rangemap = maptable[spc->getIndex()];
                    rangemap->erase(*iter);
                }
            }
            symbol->wholeCount = 0;
            symbol->mapentry.clear();
        }

        public override void removeSymbol(Symbol symbol)
        {
            if (symbol->category >= 0)
            {
                vector<Symbol*> & list(category[symbol->category]);
                list[symbol->catindex] = (Symbol*)0;
                while ((!list.empty()) && (list.back() == (Symbol*)0))
                    list.pop_back();
            }
            removeSymbolMappings(symbol);
            nametree.erase(symbol);
            delete symbol;
        }

        public override void renameSymbol(Symbol sym, string newname)
        {
            nametree.erase(sym);        // Erase under old name
            if (sym->wholeCount > 1)
                multiEntrySet.erase(sym);   // The multi-entry set is sorted by name, remove
            string oldname = sym->name;
            sym->name = newname;
            sym->displayName = newname;
            insertNameTree(sym);
            if (sym->wholeCount > 1)
                multiEntrySet.insert(sym);  // Reenter into the multi-entry set now that name is changed
        }

        public override void retypeSymbol(Symbol sym, Datatype ct)
        {
            if (ct->hasStripped())
                ct = ct->getStripped();
            if ((sym->type->getSize() == ct->getSize()) || (sym->mapentry.empty()))
            {
                // If size is the same, or no mappings nothing special to do
                sym->type = ct;
                sym->checkSizeTypeLock();
                return;
            }
            else if (sym->mapentry.size() == 1)
            {
                list<SymbolEntry>::iterator iter = sym->mapentry.back();
                if ((*iter).isAddrTied())
                {
                    // Save the starting address of map
                    Address addr((* iter).getAddr());

                    // Find the correct rangemap
                    EntryMap* rangemap = maptable[(*iter).getAddr().getSpace()->getIndex()];
                    // Remove the map entry
                    rangemap->erase(iter);
                    sym->mapentry.pop_back();   // Remove reference to map entry
                    sym->wholeCount = 0;

                    // Now we are ready to change the type
                    sym->type = ct;
                    sym->checkSizeTypeLock();
                    addMapPoint(sym, addr, Address()); // Re-add map with new size
                    return;
                }
            }
            throw RecovError("Unable to retype symbol: " + sym->name);
        }

        public override void setAttribute(Symbol sym, uint4 attr)
        {
            attr &= (Varnode::typelock | Varnode::namelock | Varnode::readonly | Varnode::incidental_copy |
                 Varnode::nolocalalias | Varnode::volatil | Varnode::indirectstorage | Varnode::hiddenretparm);
            sym->flags |= attr;
            sym->checkSizeTypeLock();
        }

        public override void clearAttribute(Symbol sym, uint4 attr)
        {
            attr &= (Varnode::typelock | Varnode::namelock | Varnode::readonly | Varnode::incidental_copy |
                 Varnode::nolocalalias | Varnode::volatil | Varnode::indirectstorage | Varnode::hiddenretparm);
            sym->flags &= ~attr;
            sym->checkSizeTypeLock();
        }

        public override void setDisplayFormat(Symbol sym, uint4 attr)
        {
            sym->setDisplayFormat(attr);
        }

        public override SymbolEntry findAddr(Address addr, Address usepoint)
        {
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                pair<EntryMap::const_iterator, EntryMap::const_iterator> res;
                if (usepoint.isInvalid())
                    res = rangemap->find(addr.getOffset(),
                             EntryMap::subsorttype(false),
                             EntryMap::subsorttype(true));
                else
                    res = rangemap->find(addr.getOffset(),
                             EntryMap::subsorttype(false),
                             EntryMap::subsorttype(usepoint));
                while (res.first != res.second)
                {
                    --res.second;
                    SymbolEntry* entry = &(*res.second);
                    if (entry->getAddr().getOffset() == addr.getOffset())
                    {
                        if (entry->inUse(usepoint))
                            return entry;
                    }
                }
            }
            return (SymbolEntry*)0;
        }

        public override SymbolEntry findContainer(Address addr,int4 size, Address usepoint)
        {
            SymbolEntry* bestentry = (SymbolEntry*)0;
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                pair<EntryMap::const_iterator, EntryMap::const_iterator> res;
                if (usepoint.isInvalid())
                    res = rangemap->find(addr.getOffset(),
                             EntryMap::subsorttype(false),
                             EntryMap::subsorttype(true));
                else
                    res = rangemap->find(addr.getOffset(),
                             EntryMap::subsorttype(false),
                             EntryMap::subsorttype(usepoint));
                int4 oldsize = -1;
                uintb end = addr.getOffset() + size - 1;
                while (res.first != res.second)
                {
                    --res.second;
                    SymbolEntry* entry = &(*res.second);
                    if (entry->getLast() >= end)
                    { // We contain the range
                        if ((entry->getSize() < oldsize) || (oldsize == -1))
                        {
                            if (entry->inUse(usepoint))
                            {
                                bestentry = entry;
                                if (entry->getSize() == size) break;
                                oldsize = entry->getSize();
                            }
                        }
                    }
                }
            }
            return bestentry;
        }

        public override SymbolEntry findClosestFit(Address addr,int4 size, Address usepoint)
        {
            SymbolEntry* bestentry = (SymbolEntry*)0;
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                pair<EntryMap::const_iterator, EntryMap::const_iterator> res;
                if (usepoint.isInvalid())
                    res = rangemap->find(addr.getOffset(),
                             EntryMap::subsorttype(false),
                             EntryMap::subsorttype(true));
                else
                    res = rangemap->find(addr.getOffset(),
                             EntryMap::subsorttype(false),
                             EntryMap::subsorttype(usepoint));
                int4 olddiff = -10000;
                int4 newdiff;

                while (res.first != res.second)
                {
                    --res.second;
                    SymbolEntry* entry = &(*res.second);
                    if (entry->getLast() >= addr.getOffset())
                    { // We contain start
                        newdiff = entry->getSize() - size;
                        if (((olddiff < 0) && (newdiff > olddiff)) ||
                            ((olddiff >= 0) && (newdiff >= 0) && (newdiff < olddiff)))
                        {
                            if (entry->inUse(usepoint))
                            {
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

        public override Funcdata findFunction(Address addr)
        {
            FunctionSymbol* sym;
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                pair<EntryMap::const_iterator, EntryMap::const_iterator> res;
                res = rangemap->find(addr.getOffset());
                while (res.first != res.second)
                {
                    SymbolEntry* entry = &(*res.first);
                    if (entry->getAddr().getOffset() == addr.getOffset())
                    {
                        sym = dynamic_cast<FunctionSymbol*>(entry->getSymbol());
                        if (sym != (FunctionSymbol*)0)
                            return sym->getFunction();
                    }
                    ++res.first;
                }
            }
            return (Funcdata*)0;
        }

        public override ExternRefSymbol findExternalRef(Address addr)
        {
            ExternRefSymbol* sym = (ExternRefSymbol*)0;
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                pair<EntryMap::const_iterator, EntryMap::const_iterator> res;
                res = rangemap->find(addr.getOffset());
                while (res.first != res.second)
                {
                    SymbolEntry* entry = &(*res.first);
                    if (entry->getAddr().getOffset() == addr.getOffset())
                    {
                        sym = dynamic_cast<ExternRefSymbol*>(entry->getSymbol());
                        break;
                    }
                    ++res.first;
                }
            }
            return sym;
        }

        public override LabSymbol findCodeLabel(Address addr)
        {
            LabSymbol* sym = (LabSymbol*)0;
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                pair<EntryMap::const_iterator, EntryMap::const_iterator> res;
                res = rangemap->find(addr.getOffset(),
                         EntryMap::subsorttype(false),
                         EntryMap::subsorttype(addr));
                while (res.first != res.second)
                {
                    --res.second;
                    SymbolEntry* entry = &(*res.second);
                    if (entry->getAddr().getOffset() == addr.getOffset())
                    {
                        if (entry->inUse(addr))
                        {
                            sym = dynamic_cast<LabSymbol*>(entry->getSymbol());
                            break;
                        }
                    }
                }
            }
            return sym;
        }

        public override SymbolEntry findOverlap(Address addr,int4 size)
        {
            EntryMap* rangemap = maptable[addr.getSpace()->getIndex()];
            if (rangemap != (EntryMap*)0)
            {
                EntryMap::const_iterator iter;
                iter = rangemap->find_overlap(addr.getOffset(), addr.getOffset() + size - 1);
                if (iter != rangemap->end())
                    return &(*iter);
            }
            return (SymbolEntry*)0;
        }

        public override void findByName(string nm,List<Symbol> res)
        {
            SymbolNameTree::const_iterator iter = findFirstByName(nm);
            while (iter != nametree.end())
            {
                Symbol* sym = *iter;
                if (sym->name != nm) break;
                res.push_back(sym);
                ++iter;
            }
        }

        public override bool isNameUsed(string nm, Scope op2)
        {
            Symbol sym((Scope*)0,nm,(Datatype*)0);
            SymbolNameTree::const_iterator iter = nametree.lower_bound(&sym);
            if (iter != nametree.end())
            {
                if ((*iter)->getName() == nm)
                    return true;
            }
            Scope* par = getParent();
            if (par == (Scope*)0 || par == op2)
                return false;
            if (par->getParent() == (Scope*)0)  // Never recurse into global scope
                return false;
            return par->isNameUsed(nm, op2);
        }

        public override Funcdata resolveExternalRefFunction(ExternRefSymbol sym);

        public override string buildVariableName(Address addr, Address pc, Datatype ct,
            int4 index, uint4 flags)
        {
            ostringstream s;
            int4 sz = (ct == (Datatype*)0) ? 1 : ct->getSize();

            if ((flags & Varnode::unaffected) != 0)
            {
                if ((flags & Varnode::return_address) != 0)
                    s << "unaff_retaddr";
                else
                {
                    string unaffname;
                    unaffname = glb->translate->getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                    if (unaffname.empty())
                    {
                        s << "unaff_";
                        s << setw(8) << setfill('0') << hex << addr.getOffset();
                    }
                    else
                        s << "unaff_" << unaffname;
                }
            }
            else if ((flags & Varnode::persist) != 0)
            {
                string spacename;
                spacename = glb->translate->getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                if (!spacename.empty())
                    s << spacename;
                else
                {
                    if (ct != (Datatype*)0)
                        ct->printNameBase(s);
                    spacename = addr.getSpace()->getName();
                    spacename[0] = toupper(spacename[0]); // Capitalize space
                    s << spacename;
                    s << hex << setfill('0') << setw(2 * addr.getAddrSize());
                    s << AddrSpace::byteToAddress(addr.getOffset(), addr.getSpace()->getWordSize());
                }
            }
            else if (((flags & Varnode::input) != 0) && (index < 0))
            { // Irregular input
                string regname;
                regname = glb->translate->getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                if (regname.empty())
                {
                    s << "in_" << addr.getSpace()->getName() << '_';
                    s << setw(8) << setfill('0') << hex << addr.getOffset();
                }
                else
                    s << "in_" << regname;
            }
            else if ((flags & Varnode::input) != 0)
            { // Regular parameter
                s << "param_" << dec << index;
            }
            else if ((flags & Varnode::addrtied) != 0)
            {
                if (ct != (Datatype*)0)
                    ct->printNameBase(s);
                string spacename = addr.getSpace()->getName();
                spacename[0] = toupper(spacename[0]); // Capitalize space
                s << spacename;
                s << hex << setfill('0') << setw(2 * addr.getAddrSize());
                s << AddrSpace::byteToAddress(addr.getOffset(), addr.getSpace()->getWordSize());
            }
            else if ((flags & Varnode::indirect_creation) != 0)
            {
                string regname;
                s << "extraout_";
                regname = glb->translate->getRegisterName(addr.getSpace(), addr.getOffset(), sz);
                if (!regname.empty())
                    s << regname;
                else
                    s << "var";
            }
            else
            {           // Some sort of local variable
                if (ct != (Datatype*)0)
                    ct->printNameBase(s);
                s << "Var" << dec << index++;
                if (findFirstByName(s.str()) != nametree.end())
                {   // If the name already exists
                    for (int4 i = 0; i < 10; ++i)
                    {   // Try bumping up the index a few times before calling makeNameUnique
                        ostringstream s2;
                        if (ct != (Datatype*)0)
                            ct->printNameBase(s2);
                        s2 << "Var" << dec << index++;
                        if (findFirstByName(s2.str()) == nametree.end())
                        {
                            return s2.str();
                        }
                    }
                }
            }
            return makeNameUnique(s.str());
        }

        public override string buildUndefinedName()
        { // We maintain a family of officially undefined names
          // so that symbols can be stored in the database without
          // having their name defined
          // We generate a name of the form '$$undefXXXXXXXX'
          // The dollar signs indicate a special name (not a legal identifier)
          // undef indicates an undefined name and the remaining
          // characters are hex digits which make the name unique
            SymbolNameTree::const_iterator iter;

            Symbol testsym((Scope*)0,"$$undefz",(Datatype*)0);

            iter = nametree.lower_bound(&testsym);
            if (iter != nametree.begin())
                --iter;
            if (iter != nametree.end())
            {
                string symname = (*iter)->getName();
                if ((symname.size() == 15) && (0 == symname.compare(0, 7, "$$undef")))
                {
                    istringstream s(symname.substr(7,8) );
                    uint4 uniq = ~((uint4)0);
                    s >> hex >> uniq;
                    if (uniq == ~((uint4)0))
                        throw new LowlevelError("Error creating undefined name");
                    uniq += 1;
                    ostringstream s2;
                    s2 << "$$undef" << hex << setw(8) << setfill('0') << uniq;
                    return s2.str();
                }
            }
            return "$$undef00000000";
        }

        public override string makeNameUnique(string nm)
        {
            SymbolNameTree::const_iterator iter = findFirstByName(nm);
            if (iter == nametree.end()) return nm; // nm is already unique

            Symbol boundsym((Scope*)0,nm + "_x99999",(Datatype*)0);
            boundsym.nameDedup = 0xffffffff;
            SymbolNameTree::const_iterator iter2 = nametree.lower_bound(&boundsym);
            uint4 uniqid;
            do
            {
                uniqid = 0xffffffff;
                --iter2;            // Last symbol whose name starts with nm
                if (iter == iter2) break;
                Symbol* bsym = *iter2;
                string bname = bsym->getName();
                bool isXForm = false;
                int4 digCount = 0;
                if ((bname.size() >= (nm.size() + 3)) && (bname[nm.size()] == '_'))
                {
                    // Collect the last id
                    int4 i = nm.size() + 1;
                    if (bname[i] == 'x')
                    {
                        i += 1;         // 5 digit form
                        isXForm = true;
                    }
                    uniqid = 0;
                    for (; i < bname.size(); ++i)
                    {
                        char dig = bname[i];
                        if (!isdigit(dig))
                        {   // Everything after '_' must be a digit, or not in our format
                            uniqid = 0xffffffff;
                            break;
                        }
                        uniqid *= 10;
                        uniqid += (dig - '0');
                        digCount += 1;
                    }
                }
                if (isXForm && (digCount != 5)) // x form, but not right number of digits
                    uniqid = 0xffffffff;
                else if ((!isXForm) && (digCount != 2))
                    uniqid = 0xffffffff;
            } while (uniqid == 0xffffffff);

            string resString;
            if (uniqid == 0xffffffff)
            {
                // no other names matching our convention
                resString = nm + "_00";     // Start a new sequence
            }
            else
            {
                uniqid += 1;
                ostringstream s;
                s << nm << '_' << dec << setfill('0');
                if (uniqid < 100)
                    s << setw(2) << uniqid;
                else
                    s << 'x' << setw(5) << uniqid;
                resString = s.str();
            }
            if (findFirstByName(resString) != nametree.end())
                throw new LowlevelError("Unable to uniquify name: " + resString);
            return resString;
        }

        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_SCOPE);
            encoder.writeString(ATTRIB_NAME, name);
            encoder.writeUnsignedInteger(ATTRIB_ID, uniqueId);
            if (getParent() != (Scope*)0) {
                encoder.openElement(ELEM_PARENT);
                encoder.writeUnsignedInteger(ATTRIB_ID, getParent()->getId());
                encoder.closeElement(ELEM_PARENT);
            }
            getRangeTree().encode(encoder);

            if (!nametree.empty())
            {
                encoder.openElement(ELEM_SYMBOLLIST);
                SymbolNameTree::const_iterator iter;
                for (iter = nametree.begin(); iter != nametree.end(); ++iter)
                {
                    Symbol* sym = *iter;
                    int4 symbolType = 0;
                    if (!sym->mapentry.empty())
                    {
                        SymbolEntry entry = *sym->mapentry.front();
                        if (entry.isDynamic())
                        {
                            if (sym->getCategory() == Symbol::union_facet)
                                continue;       // Don't save override
                            symbolType = (sym->getCategory() == Symbol::equate) ? 2 : 1;
                        }
                    }
                    encoder.openElement(ELEM_MAPSYM);
                    if (symbolType == 1)
                        encoder.writeString(ATTRIB_TYPE, "dynamic");
                    else if (symbolType == 2)
                        encoder.writeString(ATTRIB_TYPE, "equate");
                    sym->encode(encoder);
                    vector<list<SymbolEntry>::iterator>::const_iterator miter;
                    for (miter = sym->mapentry.begin(); miter != sym->mapentry.end(); ++miter)
                    {
                        SymbolEntry entry = (*(*miter));
                        entry.encode(encoder);
                    }
                    encoder.closeElement(ELEM_MAPSYM);
                }
                encoder.closeElement(ELEM_SYMBOLLIST);
            }
            encoder.closeElement(ELEM_SCOPE);
        }

        public override void decode(Decoder decoder)
        {
            //  uint4 elemId = decoder.openElement(ELEM_SCOPE);
            //  name = el->getAttributeValue("name");	// Name must already be set in the constructor
            bool rangeequalssymbols = false;

            uint4 subId = decoder.peekElement();
            if (subId == ELEM_PARENT)
            {
                decoder.skipElement();  // Skip <parent> tag processed elsewhere
                subId = decoder.peekElement();
            }
            if (subId == ELEM_RANGELIST)
            {
                RangeList newrangetree;
                newrangetree.decode(decoder);
                glb->symboltab->setRange(this, newrangetree);
            }
            else if (subId == ELEM_RANGEEQUALSSYMBOLS)
            {
                decoder.openElement();
                decoder.closeElement(subId);
                rangeequalssymbols = true;
            }
            subId = decoder.openElement(ELEM_SYMBOLLIST);
            if (subId != 0)
            {
                for (; ; )
                {
                    uint4 symId = decoder.peekElement();
                    if (symId == 0) break;
                    if (symId == ELEM_MAPSYM)
                    {
                        Symbol* sym = addMapSym(decoder);
                        if (rangeequalssymbols)
                        {
                            SymbolEntry* e = sym->getFirstWholeMap();
                            glb->symboltab->addRange(this, e->getAddr().getSpace(), e->getFirst(), e->getLast());
                        }
                    }
                    else if (symId == ELEM_HOLE)
                        decodeHole(decoder);
                    else if (symId == ELEM_COLLISION)
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
            s << "Scope " << name << endl;
            for (int4 i = 0; i < maptable.size(); ++i)
            {
                EntryMap* rangemap = maptable[i];
                if (rangemap == (EntryMap*)0) continue;
                list<SymbolEntry>::const_iterator iter, enditer;
                iter = rangemap->begin_list();
                enditer = rangemap->end_list();
                for (; iter != enditer; ++iter)
                    (*iter).printEntry(s);
            }
        }

        public override int4 getCategorySize(int4 cat)
        {
            if ((cat >= category.size()) || (cat < 0))
                return 0;
            return category[cat].size();
        }

        public override Symbol getCategorySymbol(int4 cat, int4 ind)
        {
            if ((cat >= category.size()) || (cat < 0))
                return (Symbol*)0;
            if ((ind < 0) || (ind >= category[cat].size()))
                return (Symbol*)0;
            return category[cat][ind];
        }

        public override void setCategory(Symbol sym, int4 cat, int4 ind)
        {
            if (sym->category >= 0)
            {
                vector<Symbol*> & list(category[sym->category]);
                list[sym->catindex] = (Symbol*)0;
                while ((!list.empty()) && (list.back() == (Symbol*)0))
                    list.pop_back();
            }

            sym->category = cat;
            sym->catindex = ind;
            if (cat < 0) return;
            while (category.size() <= sym->category)
                category.push_back(vector<Symbol*>());
            vector<Symbol*> & list(category[sym->category]);
            while (list.size() <= sym->catindex)
                list.push_back((Symbol*)0);
            list[sym->catindex] = sym;
        }

        /// Assign a default name (via buildVariableName) to any unnamed symbol
        /// Run through all the symbols whose name is undefined. Build a variable name, uniquify it, and
        /// rename the variable.
        /// \param base is the base index to start at for generating generic names
        public override assignDefaultNames(int4 @base)
        {
            SymbolNameTree::const_iterator iter;

            Symbol testsym((Scope*)0,"$$undef",(Datatype*)0);

            iter = nametree.upper_bound(&testsym);
            while (iter != nametree.end())
            {
                Symbol* sym = *iter;
                if (!sym->isNameUndefined()) break;
                ++iter;     // Advance before renaming
                string nm = buildDefaultName(sym, base, (Varnode*)0);
                renameSymbol(sym, nm);
            }
        }

        /// Start of symbols with more than one entry
        public IEnumerator<Symbol> beginMultiEntry()
        {
            return multiEntrySet.begin();
        }

        /// End of symbols with more than one entry
        public IEnumerator<Symbol> endMultiEntry()
        {
            return multiEntrySet.end();
        }
    }
}
