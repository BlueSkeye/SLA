using ghidra;
using Sla.DECCORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.ParamMeasure;
using static ghidra.ScoreProtoModel;

namespace Sla.DECCORE
{
    /// \brief A Symbol scope for \e local variables of a particular function.
    ///
    /// This acts like any other variable Scope, but is associated with a specific function
    /// and the address space where the function maps its local variables and parameters, typically
    /// the \e stack space. This object in addition to managing the local Symbols, builds up information
    /// about the \e stack address space: what portions of it are used for mapped local variables, what
    /// portions are used for temporary storage (not mapped), and what portion is for parameters.
    internal class ScopeLocal : ScopeInternal
    {
        /// Address space containing the local stack
        private AddrSpace space;
        /// Symbol name recommendations for specific addresses
        private List<NameRecommend> nameRecommend;
        /// Symbol name recommendations for dynamic locations
        private List<DynamicRecommend> dynRecommend;
        /// Data-types for specific storage locations
        private List<TypeRecommend> typeRecommend;
        /// Minimum offset of parameter passed (to a called function) on the stack
        private uintb minParamOffset;
        /// Maximum offset of parameter passed (to a called function) on the stack
        private uintb maxParamOffset;
        /// Marked \b true if the stack is considered to \e grow towards smaller offsets
        private bool stackGrowsNegative;
        /// True if the subset of addresses \e mapped to \b this scope has been locked
        private bool rangeLocked;

        /// Make the given RangeHint fit in the current Symbol map
        /// Shrink the RangeHint as necessary so that it fits in the mapped region of the Scope
        /// and doesn't overlap any other Symbols.  If this is not possible, return \b false.
        /// \param a is the given RangeHint to fit
        /// \return \b true if a valid adjustment was made
        private bool adjustFit(RangeHint a)
        {
            if (a.size == 0) return false;  // Nothing to fit
            if ((a.flags & Varnode::typelock) != 0) return false; // Already entered
            Address addr(space, a.start);
            uintb maxsize = getRangeTree().longestFit(addr, a.size);
            if (maxsize == 0) return false;
            if (maxsize < a.size)
            {   // Suggested range doesn't fit
                if (maxsize < a.type->getSize()) return false; // Can't shrink that match
                a.size = (int4)maxsize;
            }
            // We want ANY symbol that might be within this range
            SymbolEntry* entry = findOverlap(addr, a.size);
            if (entry == (SymbolEntry*)0)
                return true;
            if (entry->getAddr() <= addr)
            {
                // < generally shouldn't be possible
                // == we might want to check for anything in -a- after -entry-
                return false;
            }
            maxsize = entry->getAddr().getOffset() - a.start;
            if (maxsize < a.type->getSize()) return false;  // Can't shrink for this type
            a.size = maxsize;
            return true;
        }

        /// Create a Symbol entry corresponding to the given (fitted) RangeHint
        /// A name and final data-type is constructed for the RangeHint, and they are entered as
        /// a new Symbol into \b this scope.
        /// \param a is the given RangeHint to create a Symbol for
        private void createEntry(RangeHint a)
        {
            Address addr(space, a.start);
            Address usepoint;
            Datatype* ct = glb->types->concretize(a.type);
            int4 num = a.size / ct->getSize();
            if (num > 1)
                ct = glb->types->getTypeArray(num, ct);

            addSymbol("", ct, addr, usepoint);
        }

        /// Merge hints into a formal Symbol layout of the address space
        /// RangeHints from the given collection are merged into a definitive set of Symbols
        /// for \b this scope. Overlapping or open RangeHints are adjusted to form a disjoint
        /// cover of the mapped portion of the address space.  Names for the disjoint cover elements
        /// are chosen, and these form the final Symbols.
        /// \param state is the given collection of RangeHints
        /// \return \b true if there were overlaps that could not be reconciled
        private bool restructure(MapState state)
        {
            RangeHint cur;
            RangeHint* next;
            // This implementation does not allow a range
            // to contain both ~0 and 0
            bool overlapProblems = false;
            if (!state.initialize())
                return overlapProblems; // No references to stack at all

            cur = *state.next();
            while (state.getNext())
            {
                next = state.next();
                if (next->sstart < cur.sstart + cur.size)
                {   // Do the ranges intersect
                    if (cur.merge(next, space, glb->types)) // Union them
                        overlapProblems = true;
                }
                else
                {
                    if (!cur.attemptJoin(next))
                    {
                        if (cur.rangeType == RangeHint::open)
                            cur.size = next->sstart - cur.sstart;
                        if (adjustFit(cur))
                            createEntry(cur);
                        cur = *next;
                    }
                }
            }
            // The last range is artificial so we don't
            // build an entry for it
            return overlapProblems;
        }

        /// Mark all local symbols for which there are no aliases
        /// Given a set of alias starting offsets, calculate whether each Symbol within this scope might be
        /// aliased by a pointer.  The method uses locked Symbol information when available to determine
        /// how far an alias start might extend.  Otherwise a heuristic is used to determine if the Symbol
        /// is far enough away from the start of the alias to be considered unaliased.
        /// \param alias is the given set of alias starting offsets
        private void markUnaliased(List<uintb> alias)
        {
            EntryMap* rangemap = maptable[space->getIndex()];
            if (rangemap == (EntryMap*)0) return;
            list<SymbolEntry>::iterator iter, enditer;
            set<Range>::const_iterator rangeIter, rangeEndIter;
            rangeIter = getRangeTree().begin();
            rangeEndIter = getRangeTree().end();

            int4 alias_block_level = glb->alias_block_level;
            bool aliason = false;
            uintb curalias = 0;
            int4 i = 0;

            iter = rangemap->begin_list();
            enditer = rangemap->end_list();

            while (iter != enditer)
            {
                SymbolEntry & entry(*iter++);
                uintb curoff = entry.getAddr().getOffset() + entry.getSize() - 1;
                while ((i < alias.size()) && (alias[i] <= curoff))
                {
                    aliason = true;
                    curalias = alias[i++];
                }
                // Aliases shouldn't go thru unmapped regions of the local variables
                while (rangeIter != rangeEndIter)
                {
                    Range rng = *rangeIter;
                    if (rng.getSpace() == space)
                    {
                        if (rng.getFirst() > curalias && curoff >= rng.getFirst())
                            aliason = false;
                        if (rng.getLast() >= curoff) break; // Check if symbol past end of mapped range
                        if (rng.getLast() > curalias)       // If past end of range AND past last alias offset
                            aliason = false;            //    turn aliases off
                    }
                    ++rangeIter;
                }
                Symbol* symbol = entry.getSymbol();
                // Test if there is enough distance between symbol
                // and last alias to warrant ignoring the alias
                // NOTE: this is primarily to reset aliasing between
                // stack parameters and stack locals
                if (aliason && (curoff - curalias > 0xffff)) aliason = false;
                if (!aliason) symbol->getScope()->setAttribute(symbol, Varnode::nolocalalias);
                if (symbol->isTypeLocked() && alias_block_level != 0)
                {
                    if (alias_block_level == 3)
                        aliason = false;        // For this level, all locked data-types block aliases
                    else
                    {
                        type_metatype meta = symbol->getType()->getMetatype();
                        if (meta == TYPE_STRUCT)
                            aliason = false;        // Only structures block aliases
                        else if (meta == TYPE_ARRAY && alias_block_level > 1) aliason = false;// Only arrays (and structures) block aliases
                    }
                }
            }
        }

        /// Make sure all stack inputs have an associated Symbol
        /// This assigns a Symbol to any input Varnode stored in our address space, which could be
        /// a parameter but isn't in the formal prototype of the function (these should already be in
        /// the scope marked as category '0').
        private void fakeInputSymbols()
        {
            int4 lockedinputs = getCategorySize(Symbol::function_parameter);
            VarnodeDefSet::const_iterator iter, enditer;

            iter = fd->beginDef(Varnode::input);
            enditer = fd->endDef(Varnode::input);

            while (iter != enditer)
            {
                Varnode* vn = *iter++;
                bool locked = vn->isTypeLock();
                Address addr = vn->getAddr();
                if (addr.getSpace() != space) continue;
                // Only allow offsets which can be parameters
                if (!fd->getFuncProto().getParamRange().inRange(addr, 1)) continue;
                uintb endpoint = addr.getOffset() + vn->getSize() - 1;
                while (iter != enditer)
                {
                    vn = *iter;
                    if (vn->getSpace() != space) break;
                    if (endpoint < vn->getOffset()) break;
                    uintb newendpoint = vn->getOffset() + vn->getSize() - 1;
                    if (endpoint < newendpoint)
                        endpoint = newendpoint;
                    if (vn->isTypeLock())
                        locked = true;
                    ++iter;
                }
                if (!locked)
                {
                    Address usepoint;
                    //      if (!vn->addrtied())
                    // 	usepoint = vn->getUsePoint(*fd);
                    // Double check to make sure vn doesn't already have a
                    // representative symbol.  If the input prototype is locked
                    // but one of the types is TYPE_UNKNOWN, then the 
                    // corresponding varnodes won't get typelocked
                    if (lockedinputs != 0)
                    {
                        uint4 vflags = 0;
                        SymbolEntry* entry = queryProperties(vn->getAddr(), vn->getSize(), usepoint, vflags);
                        if (entry != (SymbolEntry*)0)
                        {
                            if (entry->getSymbol()->getCategory() == Symbol::function_parameter)
                                continue;       // Found a matching symbol
                        }
                    }

                    int4 size = (endpoint - addr.getOffset()) + 1;
                    Datatype* ct = fd->getArch()->types->getBase(size, TYPE_UNKNOWN);
                    try
                    {
                        addSymbol("", ct, addr, usepoint)->getSymbol();
                    }
                    catch (LowlevelError err) {
                        fd->warningHeader(err.ToString());
                    }
                    //      setCategory(sym,0,index);
                }
            }
        }

        /// Convert the given symbol to a name recommendation
        /// The symbol is stored as a name recommendation and then removed from the scope.
        /// Name recommendations are associated either with a storage address and usepoint, or a dynamic hash.
        /// The name may be reattached to a Symbol after decompilation.
        /// \param sym is the given Symbol to treat as a name recommendation
        private void addRecommendName(Symbol sym)
        {
            SymbolEntry* entry = sym->getFirstWholeMap();
            if (entry == (SymbolEntry*)0) return;
            if (entry->isDynamic())
            {
                dynRecommend.emplace_back(entry->getFirstUseAddress(), entry->getHash(), sym->getName(), sym->getId());
            }
            else
            {
                Address usepoint((AddrSpace*)0,0);
                if (!entry->getUseLimit().empty())
                {
                    Range range = entry->getUseLimit().getFirstRange();
                    usepoint = Address(range->getSpace(), range->getFirst());
                }
                nameRecommend.emplace_back(entry->getAddr(), usepoint, entry->getSize(), sym->getName(), sym->getId());
            }
            if (sym->getCategory() < 0)
                removeSymbol(sym);
        }

        /// Collect names of unlocked Symbols on the stack
        /// Turn any symbols that are \e name \e locked but not \e type \e locked into name recommendations
        /// removing the symbol in the process.  This allows the decompiler to decide on how the stack is layed
        /// out without forcing specific variables to mapped. But, if the decompiler does create a variable at
        /// the specific location, it will use the original name.
        private void collectNameRecs()
        {
            nameRecommend.clear();  // Clear out any old name recommendations
            dynRecommend.clear();

            SymbolNameTree::iterator iter = nametree.begin();
            while (iter != nametree.end())
            {
                Symbol* sym = *iter++;
                if (sym->isNameLocked() && (!sym->isTypeLocked()))
                {
                    if (sym->isThisPointer())
                    {       // If there is a "this" pointer
                        Datatype* dt = sym->getType();
                        if (dt->getMetatype() == TYPE_PTR)
                        {
                            if (((TypePointer*)dt)->getPtrTo()->getMetatype() == TYPE_STRUCT)
                            {
                                // If the "this" pointer points to a class, try to preserve the data-type
                                // even though the symbol is not preserved.
                                SymbolEntry* entry = sym->getFirstWholeMap();
                                addTypeRecommendation(entry->getAddr(), dt);
                            }
                        }
                    }
                    addRecommendName(sym);  // This deletes the symbol
                }
            }
        }

        /// Generate placeholder PTRSUB off of stack pointer
        /// For any read of the input stack pointer by a non-additive p-code op, assume this constitutes a
        /// a zero offset reference into the stack frame.  Replace the raw Varnode with the standard
        /// spacebase placeholder, PTRSUB(sp,#0), so that the data-type system can treat it as a reference.
        private void annotateRawStackPtr()
        {
            if (!fd->hasTypeRecoveryStarted()) return;
            Varnode* spVn = fd->findSpacebaseInput(space);
            if (spVn == (Varnode*)0) return;
            list<PcodeOp*>::const_iterator iter;
            vector<PcodeOp*> refOps;
            for (iter = spVn->beginDescend(); iter != spVn->endDescend(); ++iter)
            {
                PcodeOp* op = *iter;
                if (op->getEvalType() == PcodeOp::special && !op->isCall()) continue;
                OpCode opc = op->code();
                if (opc == CPUI_INT_ADD || opc == CPUI_PTRSUB || opc == CPUI_PTRADD)
                    continue;
                refOps.push_back(op);
            }
            for (int4 i = 0; i < refOps.size(); ++i)
            {
                PcodeOp* op = refOps[i];
                int4 slot = op->getSlot(spVn);
                PcodeOp* ptrsub = fd->newOpBefore(op, CPUI_PTRSUB, spVn, fd->newConstant(spVn->getSize(), 0));
                fd->opSetInput(op, ptrsub->getOut(), slot);
            }
        }

        /// Constructor
        /// \param id is the globally unique id associated with the function scope
        /// \param spc is the (stack) address space associated with this function's local variables
        /// \param fd is the function associated with these local variables
        /// \param g is the Architecture
        public ScopeLocal(uint8 id, AddrSpace spc, Funcdata fd, Architecture g)
            : base(id, fd->getName(), g)
        {
            space = spc;
            minParamOffset = ~((uintb)0);
            maxParamOffset = 0;
            rangeLocked = false;
            stackGrowsNegative = true;
            restrictScope(fd);
        }

        ~ScopeLocal()
        {
        }

        /// Get the associated (stack) address space
        public AddrSpace getSpaceId() => space;

        /// \brief Is this a storage location for \e unaffected registers
        ///
        /// \param vn is the Varnode storing an \e unaffected register
        /// \return \b true is the Varnode can be used as unaffected storage
        public bool isUnaffectedStorage(Varnode vn) => (vn->getSpace() == space);

        /// Check if a given unmapped Varnode should be treated as unaliased.
        /// Currently we treat all unmapped Varnodes as not having an alias, unless the Varnode is on the stack
        /// and the location is also used to pass parameters.  This should not be called until the second pass, in
        /// order to give markNotMapped a chance to be called.
        /// Return \b true if the Varnode can be treated as having no aliases.
        /// \param vn is the given Varnode
        /// \return \b true if there are no aliases
        public bool isUnmappedUnaliased(Varnode vn)
        {
            if (vn->getSpace() != space) return false;  // Must be in mapped local (stack) space
            if (maxParamOffset < minParamOffset) return false;  // If no min/max, then we have no know stack parameters
            if (vn->getOffset() < minParamOffset || vn->getOffset() > maxParamOffset)
                return true;
            return false;
        }

        /// Mark a specific address range is not mapped
        /// The given range can no longer hold a \e mapped local variable. This indicates the range
        /// is being used for temporary storage.
        /// \param spc is the address space holding the given range
        /// \param first is the starting offset of the given range
        /// \param sz is the number of bytes in the range
        /// \param parameter is \b true if the range is being used to store a sub-function parameter
        public void markNotMapped(AddrSpace spc, uintb first, int4 sz, bool param)
        {
            if (space != spc) return;
            uintb last = first + sz - 1;
            // Do not allow the range to cover the split point between "negative" and "positive" stack offsets
            if (last < first)       // Check for possible wrap around
                last = spc->getHighest();
            else if (last > spc->getHighest())
                last = spc->getHighest();
            if (parameter)
            {       // Everything above parameter
                if (first < minParamOffset)
                    minParamOffset = first;
                if (last > maxParamOffset)
                    maxParamOffset = last;
            }
            Address addr(space, first);
            // Remove any symbols under range
            SymbolEntry* overlap = findOverlap(addr, sz);
            while (overlap != (SymbolEntry*)0)
            { // For every overlapping entry
                Symbol* sym = overlap->getSymbol();
                if ((sym->getFlags() & Varnode::typelock) != 0)
                {
                    // If the symbol and the use are both as parameters
                    // this is likely the special case of a shared return call sharing the parameter location
                    // of the original function in which case we don't print a warning
                    if ((!parameter) || (sym->getCategory() != Symbol::function_parameter))
                        fd->warningHeader("Variable defined which should be unmapped: " + sym->getName());
                    return;
                }
                removeSymbol(sym);
                overlap = findOverlap(addr, sz);
            }
            glb->symboltab->removeRange(this, space, first, last);
        }

        // Routines that are specific to one address space
        public override void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_LOCALDB);
            encoder.writeSpace(ATTRIB_MAIN, space);
            encoder.writeBool(ATTRIB_LOCK, rangeLocked);
            ScopeInternal::encode(encoder);
            encoder.closeElement(ELEM_LOCALDB);
        }

        public override void decode(Decoder decoder)
        {
            ScopeInternal::decode(decoder);
            collectNameRecs();
        }

        public override void decodeWrappingAttributes(Decoder decoder)
        {
            rangeLocked = false;
            if (decoder.readBool(ATTRIB_LOCK))
                rangeLocked = true;
            space = decoder.readSpace(ATTRIB_MAIN);
        }

        public override string buildVariableName(Address addr, Address pc, Datatype ct, int4 index, uint4 flags)
        {
            if (((flags & (Varnode::addrtied | Varnode::persist)) == Varnode::addrtied) &&
                addr.getSpace() == space)
            {
                if (fd->getFuncProto().getLocalRange().inRange(addr, 1))
                {
                    intb start = (intb)AddrSpace::byteToAddress(addr.getOffset(), space->getWordSize());
                    sign_extend(start, addr.getAddrSize() * 8 - 1);
                    if (stackGrowsNegative)
                        start = -start;
                    ostringstream s;
                    if (ct != (Datatype*)0)
                        ct->printNameBase(s);
                    string spacename = addr.getSpace()->getName();
                    spacename[0] = toupper(spacename[0]);
                    s << spacename;
                    if (start <= 0)
                    {
                        s << 'X';       // Indicate local stack space allocated by caller
                        start = -start;
                    }
                    else
                    {
                        if ((minParamOffset < maxParamOffset) &&
                            (stackGrowsNegative ? (addr.getOffset() < minParamOffset) : (addr.getOffset() > maxParamOffset)))
                        {
                            s << 'Y';       // Indicate unusual region of stack
                        }
                    }
                    s << '_' << hex << start;
                    return makeNameUnique(s.str());
                }
            }
            return ScopeInternal::buildVariableName(addr, pc, ct, index, flags);
        }

        /// Reset the set of addresses that are considered mapped by the scope to the default
        /// This resets the discovery process for new local variables mapped to the scope's address space.
        /// Any analysis removing specific ranges from the mapped set (via markNotMapped()) is cleared.
        public void resetLocalWindow()
        {
            stackGrowsNegative = fd->getFuncProto().isStackGrowsNegative();
            minParamOffset = ~(uintb)0;
            maxParamOffset = 0;

            if (rangeLocked) return;

            RangeList localRange = fd->getFuncProto().getLocalRange();
            RangeList paramrange = fd->getFuncProto().getParamRange();

            RangeList newrange;

            set<Range>::const_iterator iter;
            for (iter = localRange.begin(); iter != localRange.end(); ++iter)
            {
                AddrSpace* spc = (*iter).getSpace();
                uintb first = (*iter).getFirst();
                uintb last = (*iter).getLast();
                newrange.insertRange(spc, first, last);
            }
            for (iter = paramrange.begin(); iter != paramrange.end(); ++iter)
            {
                AddrSpace* spc = (*iter).getSpace();
                uintb first = (*iter).getFirst();
                uintb last = (*iter).getLast();
                newrange.insertRange(spc, first, last);
            }
            glb->symboltab->setRange(this, newrange);
        }

        /// Layout mapped symbols based on Varnode information
        /// Define stack Symbols based on Varnodes.
        /// This method can be called repeatedly during decompilation. It helps propagate data-types.
        /// Unaliased symbols can optionally be marked to facilitate removal of INDIRECT ops, but
        /// this is generally done later in the process.
        /// \param aliasyes is \b true if unaliased Symbols should be marked
        public void restructureVarnode(bool aliasyes)
        {
            clearUnlockedCategory(-1);  // Clear out any unlocked entries
            MapState state(space, getRangeTree(), fd->getFuncProto().getParamRange(),
                    glb->types->getBase(1,TYPE_UNKNOWN)); // Organize list of ranges to insert

#if OPACTION_DEBUG
            if (debugon)
                state.turnOnDebug(glb);
#endif
            state.gatherVarnodes(*fd); // Gather stack type information from varnodes
            state.gatherOpen(*fd);
            state.gatherSymbols(maptable[space->getIndex()]);
            restructure(state);

            // At some point, processing mapped input symbols may be folded
            // into the above gather/restructure process, but for now
            // we just define fake symbols so that mark_unaliased will work
            clearUnlockedCategory(0);
            fakeInputSymbols();

            state.sortAlias();
            if (aliasyes)
                markUnaliased(state.getAlias());
            if (!state.getAlias().empty() && state.getAlias()[0] == 0)  // If a zero offset use of the stack pointer exists
                annotateRawStackPtr();                  // Add a special placeholder PTRSUB
        }

        /// Layout mapped symbols based on HighVariable information
        /// Define stack Symbols based on HighVariables.
        /// This method is called once at the end of decompilation to create the final set of stack Symbols after
        /// all data-type propagation has settled. It creates a consistent data-type for all Varnode instances of
        /// a HighVariable.
        public void restructureHigh()
        {               // Define stack mapping based on highs
            clearUnlockedCategory(-1);  // Clear out any unlocked entries
            MapState state(space, getRangeTree(), fd->getFuncProto().getParamRange(),
                    glb->types->getBase(1,TYPE_UNKNOWN)); // Organize list of ranges to insert

#if OPACTION_DEBUG
            if (debugon)
                state.turnOnDebug(glb);
#endif
            state.gatherHighs(*fd); // Gather stack type information from highs
            state.gatherOpen(*fd);
            state.gatherSymbols(maptable[space->getIndex()]);
            bool overlapProblems = restructure(state);

            if (overlapProblems)
                fd->warningHeader("Could not reconcile some variable overlaps");
        }

        /// \brief Change the primary mapping for the given Symbol to be a specific storage address and use point
        ///
        /// Remove any other mapping and create a mapping based on the given storage.
        /// \param sym is the given Symbol to remap
        /// \param addr is the starting address of the storage
        /// \param usepoint is the use point for the mapping
        /// \return the new mapping
        public SymbolEntry remapSymbol(Symbol sym, Address addr, Address usepoint)
        {
            SymbolEntry* entry = sym->getFirstWholeMap();
            int4 size = entry->getSize();
            if (!entry->isDynamic())
            {
                if (entry->getAddr() == addr)
                {
                    if (usepoint.isInvalid() && entry->getFirstUseAddress().isInvalid())
                        return entry;
                    if (entry->getFirstUseAddress() == usepoint)
                        return entry;
                }
            }
            removeSymbolMappings(sym);
            RangeList rnglist;
            if (!usepoint.isInvalid())
                rnglist.insertRange(usepoint.getSpace(), usepoint.getOffset(), usepoint.getOffset());
            return addMapInternal(sym, Varnode::mapped, addr, 0, size, rnglist);
        }

        /// \brief Make the primary mapping for the given Symbol, dynamic
        ///
        /// Remove any other mapping and create a new dynamic mapping based on a given
        /// size and hash
        /// \param sym is the given Symbol to remap
        /// \param hash is the dynamic hash
        /// \param usepoint is the use point for the mapping
        /// \return the new dynamic mapping
        public SymbolEntry remapSymbolDynamic(Symbol sym, uint8 hash, Address usepoint)
        {
            SymbolEntry* entry = sym->getFirstWholeMap();
            int4 size = entry->getSize();
            if (entry->isDynamic())
            {
                if (entry->getHash() == hash && entry->getFirstUseAddress() == usepoint)
                    return entry;
            }
            removeSymbolMappings(sym);
            RangeList rnglist;
            if (!usepoint.isInvalid())
                rnglist.insertRange(usepoint.getSpace(), usepoint.getOffset(), usepoint.getOffset());
            return addDynamicMapInternal(sym, Varnode::mapped, hash, 0, size, rnglist);
        }

        /// \brief Run through name recommendations, checking if any match unnamed symbols
        ///
        /// Unlocked symbols that are presented to the decompiler are stored off as \e recommended names. These
        /// can be reattached after the decompiler makes a determination of what the final Symbols are.
        /// This method runs through the recommended names and checks if they can be applied to an existing
        /// unnamed Symbol.
        public void recoverNameRecommendationsForSymbols()
        {
            Address param_usepoint = fd->getAddress() - 1;
            list<NameRecommend>::const_iterator iter;
            for (iter = nameRecommend.begin(); iter != nameRecommend.end(); ++iter)
            {
                Address addr = (*iter).getAddr();
                Address usepoint = (*iter).getUseAddr();
                int4 size = (*iter).getSize();
                Symbol* sym;
                Varnode* vn = (Varnode*)0;
                if (usepoint.isInvalid())
                {
                    SymbolEntry* entry = findOverlap(addr, size);   // Recover any Symbol regardless of usepoint
                    if (entry == (SymbolEntry*)0) continue;
                    if (entry->getAddr() != addr)       // Make sure Symbol has matching address
                        continue;
                    sym = entry->getSymbol();
                    if ((sym->getFlags() & Varnode::addrtied) == 0)
                        continue;               // Symbol must be address tied to match this name recommendation
                    vn = fd->findLinkedVarnode(entry);
                }
                else
                {
                    if (usepoint == param_usepoint)
                        vn = fd->findVarnodeInput(size, addr);
                    else
                        vn = fd->findVarnodeWritten(size, addr, usepoint);
                    if (vn == (Varnode*)0) continue;
                    sym = vn->getHigh()->getSymbol();
                    if (sym == (Symbol*)0) continue;
                    if ((sym->getFlags() & Varnode::addrtied) != 0)
                        continue;               // Cannot use untied varnode as primary map for address tied symbol
                    SymbolEntry* entry = sym->getFirstWholeMap();
                    // entry->getAddr() does not need to match address of the recommendation
                    if (entry->getSize() != size) continue;
                }
                if (!sym->isNameUndefined()) continue;
                renameSymbol(sym, makeNameUnique((*iter).getName()));
                setSymbolId(sym, (*iter).getSymbolId());
                setAttribute(sym, Varnode::namelock);
                if (vn != (Varnode*)0)
                {
                    fd->remapVarnode(vn, sym, usepoint);
                }
            }

            if (dynRecommend.empty()) return;

            list<DynamicRecommend>::const_iterator dyniter;
            DynamicHash dhash;
            for (dyniter = dynRecommend.begin(); dyniter != dynRecommend.end(); ++dyniter)
            {
                dhash.clear();
                DynamicRecommend dynEntry = *dyniter;
                Varnode* vn = dhash.findVarnode(fd, dynEntry.getAddress(), dynEntry.getHash());
                if (vn == (Varnode*)0) continue;
                if (vn->isAnnotation()) continue;
                Symbol* sym = vn->getHigh()->getSymbol();
                if (sym == (Symbol*)0) continue;
                if (sym->getScope() != this) continue;
                if (!sym->isNameUndefined()) continue;
                renameSymbol(sym, makeNameUnique(dynEntry.getName()));
                setAttribute(sym, Varnode::namelock);
                setSymbolId(sym, dynEntry.getSymbolId());
                fd->remapDynamicVarnode(vn, sym, dynEntry.getAddress(), dynEntry.getHash());
            }
        }

        /// Try to apply recommended data-type information
        /// Run through the recommended list, search for an input Varnode matching the storage address
        /// and try to apply the data-type to it.  Do not override existing type lock.
        public void applyTypeRecommendations()
        {
            list<TypeRecommend>::const_iterator iter;
            for (iter = typeRecommend.begin(); iter != typeRecommend.end(); ++iter)
            {
                Datatype* dt = (*iter).getType();
                Varnode* vn = fd->findVarnodeInput(dt->getSize(), (*iter).getAddress());
                if (vn != (Varnode*)0)
                    vn->updateType(dt, true, false);
            }
        }

        /// Are there data-type recommendations
        public bool hasTypeRecommendations() => !typeRecommend.empty();

        /// Add a new data-type recommendation
        /// Associate a data-type with a particular storage address. If we see an input Varnode at this address,
        /// if no other info is available, the given data-type is applied.
        /// \param addr is the storage address
        /// \param dt is the given data-type
        public void addTypeRecommendation(Address addr, Datatype dt)
        {
            typeRecommend.push_back(TypeRecommend(addr, dt));
        }
    }
}
