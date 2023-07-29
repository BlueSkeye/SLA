using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A high-level variable modeled as a list of low-level variables, each written once
    ///
    /// In the Static Single Assignment (SSA) representation of a function's data-flow, the Varnode
    /// object represents a variable node. This is a \b low-level \b variable: it is written to
    /// at most once, and there is 1 or more reads.  A \b high-level \b variable, in the source
    /// language may be written to multiple times. We model this idea as a list of Varnode objects, where
    /// a different Varnode holds the value of the variable for different parts of the code. The range(s)
    /// of code for which a single Varnode holds the high-level variable's value is the \b cover or \b range
    /// of that Varnode and is modeled by the class Cover.  Within a high-level variable, HighVariable,
    /// the covers of member Varnode objects should not intersect, as that represents the variable holding
    /// two or more different values at the same place in the code. The HighVariable inherits a cover
    /// which is the union of the covers of its Varnodes.
    internal class HighVariable
    {
        /// \brief Dirtiness flags for a HighVariable
        ///
        /// The HighVariable inherits its Cover, its data-type, and other boolean properties from its Varnodes.
        /// The object holds these explicitly, but the values may become stale as the data-flow transforms.
        /// So we keep track of when these inherited values are \e dirty
        [Flags()]
        public enum DitinessFlags
        {
            /// Boolean properties for the HighVariable are dirty
            flagsdirty = 1,
            /// The name representative for the HighVariable is dirty
            namerepdirty = 2,
            /// The data-type for the HighVariable is dirty
            typedirty = 4,
            /// The cover for the HighVariable is dirty
            coverdirty = 8,
            /// The symbol attachment is dirty
            symboldirty = 0x10,
            /// There exists at least 1 COPY into \b this HighVariable from other HighVariables
            copy_in1 = 0x20,
            /// There exists at least 2 COPYs into \b this HighVariable from other HighVariables
            copy_in2 = 0x40,
            /// Set if a final data-type is locked in and dirtying is disabled
            type_finalized = 0x80,
            /// Set if part of a multi-entry Symbol but did not get merged with other SymbolEntrys
            unmerged = 0x100,
            /// Set if intersections with other HighVariables needs to be recomputed
            intersectdirty = 0x200,
            /// Set if extended cover needs to be recomputed
            extendcoverdirty = 0x400
        }

        // friend class Varnode;
        // friend class Merge;
        // friend class VariablePiece;
        // friend class HighIntersectTest;
        /// The member Varnode objects making up \b this HighVariable
        private List<Varnode> inst;
        /// Number of different speculative merge classes in \b this
        private int4 numMergeClasses;
        /// Dirtiness flags
        private /*mutable*/ uint4 highflags;
        /// Boolean properties inherited from Varnode members
        private /*mutable*/ uint4 flags;
        /// The data-type for \b this
        private /*mutable*/ Datatype type;
        /// The storage location used to generate a Symbol name
        private /*mutable*/ Varnode nameRepresentative;
        /// The ranges of code addresses covered by this HighVariable
        private /*mutable*/ Cover internalCover;
        /// Additional info about intersections with other pieces (if non-null)
        private /*mutable*/ VariablePiece piece;
        /// The Symbol \b this HighVariable is tied to
        private /*mutable*/ Symbol symbol;
        /// -1=perfect symbol match >=0, offset
        private /*mutable*/ int4 symboloffset;

        /// Find the index of a specific Varnode member
        /// Find the index, for use with getInstance(), that will retrieve the given Varnode member
        /// \param vn is the given Varnode member
        /// \return the index of the member or -1 if it is not a member
        private int4 instanceIndex(Varnode vn)
        {
            int4 i;

            for (i = 0; i < inst.size(); ++i)
                if (inst[i] == vn) return i;

            return -1;
        }

        /// (Re)derive boolean properties of \b this from the member Varnodes
        /// Only update if flags are marked as \e dirty.
        /// Generally if any member Varnode possesses the property, \b this HighVariable should
        /// inherit it.  The Varnode::typelock field is not set here, but in updateType().
        private void updateFlags()
        {
            if ((highflags & flagsdirty) == 0) return; // flags are up to date

            vector<Varnode*>::const_iterator iter;
            uint4 fl = 0;

            for (iter = inst.begin(); iter != inst.end(); ++iter)
                fl |= (*iter)->getFlags();

            // Keep these flags
            flags &= (Varnode::mark | Varnode::typelock);
            // Update all but these
            flags |= fl & ~(Varnode::mark | Varnode::directwrite | Varnode::typelock);
            highflags &= ~flagsdirty; // Clear the dirty flag
        }

        /// (Re)derive the internal cover of \b this from the member Varnodes
        /// Only update if the cover is marked as \e dirty.
        /// Merge the covers of all Varnode instances.
        private void updateInternalCover()
        {
            if ((highflags & coverdirty) != 0)
            {
                internalCover.clear();
                if (inst[0]->hasCover())
                {
                    for (int4 i = 0; i < inst.size(); ++i)
                        internalCover.merge(*inst[i]->getCover());
                }
                highflags &= ~coverdirty;
            }
        }

        /// (Re)derive the external cover of \b this, as a union of internal covers
        /// This is \b only called by the Merge class which knows when to call it properly.
        private void updateCover()
        {
            if (piece == (VariablePiece*)0)
                updateInternalCover();
            else
            {
                piece->updateIntersections();
                piece->updateCover();
            }
        }

        /// (Re)derive the data-type for \b this from the member Varnodes
        /// Only update if the data-type is marked as \e dirty.
        /// Get the most locked, most specific data-type from member Varnode objects.
        private void updateType()
        {
            Varnode* vn;

            if ((highflags & typedirty) == 0) return; // Type is up to date
            highflags &= ~typedirty; // Mark type as clean
            if ((highflags & type_finalized) != 0) return;  // Type has been finalized
            vn = getTypeRepresentative();

            type = vn->getType();
            if (type->hasStripped())
            {
                if (type->getMetatype() == TYPE_PARTIALUNION)
                {
                    if (symbol != (Symbol*)0 && symboloffset != -1)
                    {
                        type_metatype meta = symbol->getType()->getMetatype();
                        if (meta != TYPE_STRUCT && meta != TYPE_UNION)  // If partial union does not have a bigger backing symbol
                            type = type->getStripped();         // strip the partial union
                    }
                }
                else
                    type = type->getStripped();
            }
            // Update lock flags
            flags &= ~Varnode::typelock;
            if (vn->isTypeLock())
                flags |= Varnode::typelock;
        }

        /// (Re)derive the Symbol and offset for \b this from member Varnodes
        private void updateSymbol()
        {
            if ((highflags & symboldirty) == 0) return; // flags are up to date
            highflags &= ~((uint4)symboldirty);
            vector<Varnode*>::const_iterator iter;
            symbol = (Symbol*)0;

            for (iter = inst.begin(); iter != inst.end(); ++iter)
            {
                Varnode* vn = *iter;
                if (vn->getSymbolEntry() != (SymbolEntry*)0)
                {
                    setSymbol(vn);
                    return;
                }
            }
        }

        /// Mark the existence of one COPY into \b this
        private void setCopyIn1() 
        {
            highflags |= copy_in1;
        }

        /// Mark the existence of two COPYs into \b this
        private void setCopyIn2() 
        {
            highflags |= copy_in2;
        }

        /// Clear marks indicating COPYs into \b this
        private void clearCopyIns() 
        {
            highflags &= ~(copy_in1 | copy_in2);
        }

        /// Is there at least one COPY into \b this
        private bool hasCopyIn1() => ((highflags&copy_in1)!= 0);

        /// Is there at least two COPYs into \b this
        private bool hasCopyIn2() => ((highflags&copy_in2)!= 0);

        /// Remove a member Varnode from \b this
        /// Search for the given Varnode and cut it out of the list, marking all properties as \e dirty.
        /// \param vn is the given Varnode member to remove
        private void remove(Varnode vn)
        {
            vector<Varnode*>::iterator iter;

            iter = lower_bound(inst.begin(), inst.end(), vn, compareJustLoc);
            for (; iter != inst.end(); ++iter)
            {
                if (*iter == vn)
                {
                    inst.erase(iter);
                    highflags |= (flagsdirty | namerepdirty | coverdirty | typedirty);
                    if (vn->getSymbolEntry() != (SymbolEntry*)0)
                        highflags |= symboldirty;
                    if (piece != (VariablePiece*)0)
                        piece->markExtendCoverDirty();
                    return;
                }
            }
        }

        /// Merge another HighVariable into \b this
        /// The lists of members are merged and the other HighVariable is deleted.
        /// \param tv2 is the other HighVariable to merge into \b this
        /// \param isspeculative is \b true to keep the new members in separate \e merge classes
        private void mergeInternal(HighVariable tv2, bool isspeculative)
        {
            int4 i;

            highflags |= (flagsdirty | namerepdirty | typedirty);
            if (tv2->symbol != (Symbol*)0)
            {       // Check if we inherit a Symbol
                if ((tv2->highflags & symboldirty) == 0)
                {
                    symbol = tv2->symbol;           // Overwrite our Symbol (assume it is the same)
                    symboloffset = tv2->symboloffset;
                    highflags &= ~((uint4)symboldirty); // Mark that we are not symbol dirty
                }
            }

            if (isspeculative)
            {
                for (i = 0; i < tv2->inst.size(); ++i)
                {
                    Varnode* vn = tv2->inst[i];
                    vn->setHigh(this, vn->getMergeGroup() + numMergeClasses);
                }
                numMergeClasses += tv2->numMergeClasses;
            }
            else
            {
                if ((numMergeClasses != 1) || (tv2->numMergeClasses != 1))
                    throw new LowlevelError("Making a non-speculative merge after speculative merges have occurred");
                for (i = 0; i < tv2->inst.size(); ++i)
                {
                    Varnode* vn = tv2->inst[i];
                    vn->setHigh(this, vn->getMergeGroup());
                }
            }
            vector<Varnode*> instcopy(inst);
            inst.resize(inst.size() + tv2->inst.size(), (Varnode*)0);
            std::merge(instcopy.begin(), instcopy.end(), tv2->inst.begin(), tv2->inst.end(), inst.begin(), compareJustLoc);
            tv2->inst.clear();

            if (((highflags & coverdirty) == 0) && ((tv2->highflags & coverdirty) == 0))
                internalCover.merge(tv2->internalCover);
            else
                highflags |= coverdirty;

            delete tv2;
        }

        /// Merge with another HighVariable taking into account groups
        /// The HighVariables are merged internally as with mergeInternal.  If \b this is part of a VariableGroup,
        /// extended covers of the group may be affected.  If both HighVariables are part of separate groups,
        /// the groups are combined into one, which may induce additional HighVariable pairs within the group to be merged.
        /// In all cases, the other HighVariable is deleted.
        /// \param tv2 is the other HighVariable to merge into \b this
        /// \param testCache if non-null is a cache of intersection tests that must be updated to reflect the merge
        /// \param isspeculative is \b true to keep the new members in separate \e merge classes
        private void merge(HighVariable tv2, HighIntersectTest testCache, bool isspeculative)
        {
            if (tv2 == this) return;

            if (testCache != (HighIntersectTest*)0)
                testCache->moveIntersectTests(this, tv2);
            if (piece == (VariablePiece*)0 && tv2->piece == (VariablePiece*)0)
            {
                mergeInternal(tv2, isspeculative);
                return;
            }
            if (tv2->piece == (VariablePiece*)0)
            {
                // Keep group that this is already in
                piece->markExtendCoverDirty();
                mergeInternal(tv2, isspeculative);
                return;
            }
            if (piece == (VariablePiece*)0)
            {
                // Move ownership of the VariablePiece object from the HighVariable that will be freed
                transferPiece(tv2);
                piece->markExtendCoverDirty();
                mergeInternal(tv2, isspeculative);
                return;
            }
            // Reaching here both HighVariables are part of a group
            if (isspeculative)
                throw new LowlevelError("Trying speculatively merge variables in separate groups");
            vector<HighVariable*> mergePairs;
            piece->mergeGroups(tv2->piece, mergePairs);
            for (int4 i = 0; i < mergePairs.size(); i += 2)
            {
                HighVariable* high1 = mergePairs[i];
                HighVariable* high2 = mergePairs[i + 1];
                if (testCache != (HighIntersectTest*)0)
                    testCache->moveIntersectTests(high1, high2);
                high1->mergeInternal(high2, isspeculative);
            }
            piece->markIntersectionDirty();
        }

        /// Update Symbol information for \b this from the given member Varnode
        /// The given Varnode \b must be a member and \b must have a non-null SymbolEntry
        private void setSymbol(Varnode vn)
        {
            SymbolEntry* entry = vn->getSymbolEntry();
            if (symbol != (Symbol*)0 && symbol != entry->getSymbol())
            {
                if ((highflags & symboldirty) == 0)
                {
                    ostringstream s;
                    s << "Symbols \"" << symbol->getName() << "\" and \"" << entry->getSymbol()->getName();
                    s << "\" assigned to the same variable";
                    throw new LowlevelError(s.str());
                }
            }
            symbol = entry->getSymbol();
            if (vn->isProtoPartial() && piece != (VariablePiece*)0)
            {
                symboloffset = piece->getOffset() + piece->getGroup()->getSymbolOffset();
            }
            else if (entry->isDynamic())    // Dynamic symbols (that aren't partials) match whole variable
                symboloffset = -1;
            else if (symbol->getCategory() == Symbol::equate)
                symboloffset = -1;          // For equates, we don't care about size
            else if (symbol->getType()->getSize() == vn->getSize() &&
                entry->getAddr() == vn->getAddr() && !entry->isPiece())
                symboloffset = -1;          // A matching entry
            else
            {
                symboloffset = vn->getAddr().overlapJoin(0, entry->getAddr(), symbol->getType()->getSize()) + entry->getOffset();
            }

            if (type != (Datatype*)0 && type->getMetatype() == TYPE_PARTIALUNION)
                highflags |= typedirty;
            highflags &= ~((uint4)symboldirty);     // We are no longer dirty
        }

        /// Attach a reference to a Symbol to \b this
        /// Link information to \b this from a Symbol that is not attached to a member Varnode.
        /// This only works for a HighVariable with a constant member Varnode.  This used when there
        /// is a constant address reference to the Symbol and the Varnode holds the reference, not
        /// the actual value of the Symbol.
        /// \param sym is the given Symbol to attach
        /// \param off is the byte offset into the Symbol of the reference
        private void setSymbolReference(Symbol sym, int4 off)
        {
            symbol = sym;
            symboloffset = off;
            highflags &= ~((uint4)symboldirty);
        }

        /// Transfer ownership of another's VariablePiece to \b this
        private void transferPiece(HighVariable tv2)
        {
            piece = tv2->piece;
            tv2->piece = (VariablePiece*)0;
            piece->setHigh(this);
            highflags |= (tv2->highflags & (intersectdirty | extendcoverdirty));
            tv2->highflags &= ~(uint4)(intersectdirty | extendcoverdirty);
        }

        /// Mark the boolean properties as \e dirty
        private void flagsDirty() 
        {
            highflags |= flagsdirty | namerepdirty;
        }

        /// Mark the cover as \e dirty
        /// The internal cover is marked as dirty. If \b this is a piece of a VariableGroup, it and all the other
        /// HighVariables it intersects with are marked as having a dirty extended cover.
        private void coverDirty()
        {
            highflags |= coverdirty;
            if (piece != (VariablePiece*)0)
                piece->markExtendCoverDirty();
        }

        /// Mark the data-type as \e dirty
        private void typeDirty()
        {
            highflags |= typedirty;
        }

        /// Mark the symbol as \e dirty
        private void symbolDirty()
        {
            highflags |= symboldirty;
        }

        /// Mark \b this as having merge problems
        private void setUnmerged() 
        {
            highflags |= unmerged;
        }

        /// Is the cover returned by getCover() up-to-date
        /// The cover could either by the internal one or the extended one if \b this is part of a Variable Group.
        /// \return \b true if the cover needs to be recomputed.
        private bool isCoverDirty()
        {
            return ((highflags & (coverdirty | extendcoverdirty)) != 0);
        }

        /// Construct a HighVariable with a single member Varnode
        /// The new instance starts off with no associate Symbol and all properties marked as \e dirty.
        /// \param vn is the single Varnode member
        public HighVariable(Varnode vn)
        {
            numMergeClasses = 1;
            highflags = flagsdirty | namerepdirty | typedirty | coverdirty;
            flags = 0;
            type = (Datatype*)0;
            piece = (VariablePiece*)0;
            symbol = (Symbol*)0;
            nameRepresentative = (Varnode*)0;
            symboloffset = -1;
            inst.push_back(vn);
            vn->setHigh(this, numMergeClasses - 1);
            if (vn->getSymbolEntry() != (SymbolEntry*)0)
                setSymbol(vn);
        }

        ~HighVariable()
        {
            if (piece != (VariablePiece*)0)
                delete piece;
        }

        /// Get the data-type
        public Datatype getType() 
        {
            updateType();
            return type;
        }

        /// Get cover data for \b this variable
        /// The returns the internal cover unless \b this is part of a VariableGroup, in which case the
        /// extended cover is returned.
        /// \return the cover associated with \b this variable
        public Cover getCover()
        {
            if (piece == (VariablePiece*)0)
                return internalCover;
            return piece->getCover();
        }

        /// Get the Symbol associated with \b this or null
        public Symbol getSymbol() 
        {
            updateSymbol();
            return symbol;
        }

        /// Get the SymbolEntry mapping to \b this or null
        /// Assuming there is a Symbol attached to \b this, run through the Varnode members
        /// until we find one with a SymbolEntry corresponding to the Symbol and return it.
        /// \return the SymbolEntry that mapped the Symbol to \b this or null if no Symbol is attached
        public SymbolEntry getSymbolEntry()
        {
            for (int4 i = 0; i < inst.size(); ++i)
            {
                SymbolEntry* entry = inst[i]->getSymbolEntry();
                if (entry != (SymbolEntry*)0 && entry->getSymbol() == symbol)
                    return entry;
            }
            return (SymbolEntry*)0;
        }

        /// Get the Symbol offset associated with \b this
        public int4 getSymbolOffset() => symboloffset;

        /// Get the number of member Varnodes \b this has
        public int4 numInstances() => inst.size();

        /// Get the i-th member Varnode
        public Varnode getInstance(int4 i) => inst[i];

        /// Set a final datatype for \b this variable
        /// The data-type its dirtying mechanism is disabled.  The data-type will not change, unless
        /// this method is called again.
        /// \param tp is the data-type to set
        public void finalizeDatatype(Datatype tp)
        {
            type = tp;
            if (type->hasStripped())
            {
                if (type->getMetatype() == TYPE_PARTIALUNION)
                {
                    if (symbol != (Symbol*)0 && symboloffset != -1)
                    {
                        type_metatype meta = symbol->getType()->getMetatype();
                        if (meta != TYPE_STRUCT && meta != TYPE_UNION)  // If partial union does not have a bigger backing symbol
                            type = type->getStripped();         // strip the partial union
                    }
                }
                else
                    type = type->getStripped();
            }
            highflags |= type_finalized;
        }

        /// Put \b this and another HighVariable in the same intersection group
        /// If one of the HighVariables is already in a group, the other HighVariable is added to this group.
        /// \param off is the relative byte offset of \b this with the other HighVariable
        /// \param hi2 is the other HighVariable
        public void groupWith(int4 off, HighVariable hi2)
        {
            if (piece == (VariablePiece*)0 && hi2->piece == (VariablePiece*)0)
            {
                hi2->piece = new VariablePiece(hi2, 0);
                piece = new VariablePiece(this, off, hi2);
                hi2->piece->markIntersectionDirty();
                return;
            }
            if (piece == (VariablePiece*)0)
            {
                if ((hi2->highflags & intersectdirty) == 0)
                    hi2->piece->markIntersectionDirty();
                highflags |= intersectdirty | extendcoverdirty;
                off += hi2->piece->getOffset();
                piece = new VariablePiece(this, off, hi2);
            }
            else if (hi2->piece == (VariablePiece*)0)
            {
                int4 hi2Off = piece->getOffset() - off;
                if (hi2Off < 0)
                {
                    piece->getGroup()->adjustOffsets(-hi2Off);
                    hi2Off = 0;
                }
                if ((highflags & intersectdirty) == 0)
                    piece->markIntersectionDirty();
                hi2->highflags |= intersectdirty | extendcoverdirty;
                hi2->piece = new VariablePiece(hi2, hi2Off, this);
            }
            else
            {
                int4 offDiff = hi2->piece->getOffset() + off - piece->getOffset();
                if (offDiff != 0)
                    piece->getGroup()->adjustOffsets(offDiff);
                hi2->piece->getGroup()->combineGroups(piece->getGroup());
                hi2->piece->markIntersectionDirty();
            }
        }

        /// Transfer \b symbol offset of \b this to the VariableGroup
        /// If \b this is part of a larger group and has had its \b symboloffset set, it can be used
        /// to calculate the \b symboloffset of other HighVariables in the same group, by writing it
        /// to the common VariableGroup object.
        public void establishGroupSymbolOffset()
        {
            VariableGroup* group = piece->getGroup();
            int4 off = symboloffset;
            if (off < 0)
                off = 0;
            off -= piece->getOffset();
            if (off < 0)
                throw new LowlevelError("Symbol offset is incompatible with VariableGroup");
            group->setSymbolOffset(off);
        }

        /// \brief Print details of the cover for \b this (for debug purposes)
        ///
        /// \param s is the output stream
        public void printCover(TextWriter s)
        {
            if ((highflags&HighVariable::coverdirty)== 0)
                internalCover.print(s);
            else s << "Cover dirty";
        }

        /// Print information about \b this HighVariable to stream
        /// This is generally used for debug purposes.
        /// \param s is the output stream
        public void printInfo(TextWriter s)
        {
            vector<Varnode*>::const_iterator viter;
            Varnode* vn;

            updateType();
            if (symbol == (Symbol*)0)
            {
                s << "Variable: UNNAMED" << endl;
            }
            else
            {
                s << "Variable: " << symbol->getName();
                if (symboloffset != -1)
                    s << "(partial)";
                s << endl;
            }
            s << "Type: ";
            type->printRaw(s);
            s << "\n\n";

            for (viter = inst.begin(); viter != inst.end(); ++viter)
            {
                vn = *viter;
                s << dec << vn->getMergeGroup() << ": ";
                vn->printInfo(s);
            }
        }

        /// Check if \b this HighVariable can be named
        /// All Varnode objects are assigned a HighVariable, including those that don't get names like
        /// indirect variables, constants, and annotations.  Determine if \b this, as inherited from its
        /// member Varnodes, can have a name.
        /// \return \b true if \b this can have a name
        public bool hasName()
        {
            bool indirectonly = true;
            for (int4 i = 0; i < inst.size(); ++i)
            {
                Varnode* vn = inst[i];
                if (!vn->hasCover())
                {
                    if (inst.size() > 1)
                        throw new LowlevelError("Non-coverable varnode has been merged");
                    return false;
                }
                if (vn->isImplied())
                {
                    if (inst.size() > 1)
                        throw new LowlevelError("Implied varnode has been merged");
                    return false;
                }
                if (!vn->isIndirectOnly())
                    indirectonly = false;
            }
            if (isUnaffected())
            {
                if (!isInput()) return false;
                if (indirectonly) return false;
                Varnode* vn = getInputVarnode();
                if (!vn->isIllegalInput())
                { // A leftover unaff illegal input gets named
                    if (vn->isSpacebase())  // A legal input, unaff, gets named
                        return false;       // Unless it is the stackpointer
                }
            }
            return true;
        }

        /// Find the first address tied member Varnode
        /// This should only be called if isAddrTied() returns \b true. If there is no address tied
        /// member, this will throw an exception.
        /// \return the first address tied member
        public Varnode getTiedVarnode()
        {
            int4 i;

            for (i = 0; i < inst.size(); ++i)
                if (inst[i]->isAddrTied())
                    return inst[i];

            throw new LowlevelError("Could not find address-tied varnode");
        }

        /// Find (the) input member Varnode
        /// This should only be called if isInput() returns \b true. If there is no input
        /// member, this will throw an exception.
        /// \return the input Varnode member
        public Varnode getInputVarnode()
        {
            for (int4 i = 0; i < inst.size(); ++i)
                if (inst[i]->isInput())
                    return inst[i];
            throw new LowlevelError("Could not find input varnode");
        }

        /// Get a member Varnode with the strongest data-type
        /// Find the member Varnode with the most \e specialized data-type, handling \e bool specially.
        /// Boolean data-types are \e specialized in the data-type lattice, but not all byte values are boolean values.
        /// Within the Varnode/PcodeOp tree, the \e bool data-type can only propagate to a Varnode if it is verified to
        /// only take the boolean values 0 and 1. Since the data-type representative represents the type of all
        /// instances, if any instance is not boolean, then the HighVariable cannot be boolean, even though \e bool
        /// is more specialized. This method uses Datatype::typeOrderBool() to implement the special handling.
        /// \return the representative member
        public Varnode getTypeRepresentative()
        {
            vector<Varnode*>::const_iterator iter;
            Varnode* vn,*rep;

            iter = inst.begin();
            rep = *iter;
            ++iter;
            for (; iter != inst.end(); ++iter)
            {
                vn = *iter;
                if (rep->isTypeLock() != vn->isTypeLock())
                {
                    if (vn->isTypeLock())
                        rep = vn;
                }
                else if (0 > vn->getType()->typeOrderBool(*rep->getType()))
                    rep = vn;
            }
            return rep;
        }

        /// Get a member Varnode that dictates the naming of \b this HighVariable
        /// Members are scored based the properties that are most dominating in choosing a name.
        /// \return the highest scoring Varnode member
        public Varnode getNameRepresentative()
        {
            if ((highflags & namerepdirty) == 0)
                return nameRepresentative;      // Name representative is up to date
            highflags &= ~namerepdirty;

            vector<Varnode*>::const_iterator iter;
            Varnode* vn;

            iter = inst.begin();
            nameRepresentative = *iter;
            ++iter;
            for (; iter != inst.end(); ++iter)
            {
                vn = *iter;
                if (compareName(nameRepresentative, vn))
                    nameRepresentative = vn;
            }
            return nameRepresentative;
        }

        /// Get the number of speculative merges for \b this
        public int4 getNumMergeClasses() => numMergeClasses;

        /// Return \b true if \b this is mapped
        public bool isMapped() 
        {
            updateFlags();
            return ((flags & Varnode::mapped) != 0);
        }

        /// Return \b true if \b this is a global variable
        public bool isPersist() 
        {
            updateFlags();
            return ((flags & Varnode::persist) != 0);
        }

        /// Return \b true if \b this is \e address \e ties
        public bool isAddrTied() 
        {
            updateFlags();
            return ((flags & Varnode::addrtied) != 0);
        }

        /// Return \b true if \b this is an input variable
        public bool isInput() 
        {
            updateFlags();
            return ((flags & Varnode::input) != 0);
        }

        /// Return \b true if \b this is an implied variable
        public bool isImplied()
        {
            updateFlags();
            return ((flags & Varnode::implied) != 0);
        }

        /// Return \b true if \b this is a \e spacebase
        public bool isSpacebase() 
        {
            updateFlags();
            return ((flags & Varnode::spacebase) != 0);
        }

        /// Return \b true if \b this is a constant
        public bool isConstant() 
        {
            updateFlags();
            return ((flags & Varnode::constant) != 0);
        }

        /// Return \b true if \b this is an \e unaffected register
        public bool isUnaffected() 
        {
            updateFlags();
            return ((flags & Varnode::unaffected) != 0);
        }

        /// Return \b true if \b this is an extra output
        public bool isExtraOut()
        {
            updateFlags();
            return ((flags & (Varnode::indirect_creation | Varnode::addrtied)) == Varnode::indirect_creation);
        }

        /// Return \b true if \b this is a piece concatenated into a larger whole
        public bool isProtoPartial() 
        {
            updateFlags();
            return ((flags & Varnode::proto_partial) != 0);
        }

        /// Set the mark on this variable
        public void setMark()
        {
            flags |= Varnode::mark;
        }

        /// Clear the mark on this variable
        public void clearMark()
        {
            flags &= ~Varnode::mark;
        }

        /// Return \b true if \b this is marked
        public bool isMark() => ((flags&Varnode::mark)!= 0);

        /// Return \b true if \b this has merge problems
        public bool isUnmerged() => ((highflags&unmerged)!= 0);

        /// \brief Determine if \b this HighVariable has an associated cover.
        ///
        /// Constant and annotation variables do not have a cover
        /// \return \b true if \b this has a cover
        public bool hasCover()
        {
            updateFlags();
            return ((flags & (Varnode::constant | Varnode::annotation | Varnode::insert)) == Varnode::insert);
        }

        /// Return \b true if \b this has no member Varnode
        public bool isUnattached() => inst.empty();

        /// Return \b true if \b this is \e typelocked
        public bool isTypeLock() 
        {
            updateType();
            return ((flags & Varnode::typelock) != 0);
        }

        /// Return \b true if \b this is \e namelocked
        public bool isNameLock()
        {
            updateFlags();
            return ((flags & Varnode::namelock) != 0);
        }

        /// Encode \b this variable to stream as a \<high> element
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            Varnode* vn = getNameRepresentative(); // Get representative varnode
            encoder.openElement(ELEM_HIGH);
            encoder.writeUnsignedInteger(ATTRIB_REPREF, vn->getCreateIndex());
            if (isSpacebase() || isImplied()) // This is a special variable
                encoder.writeString(ATTRIB_CLASS, "other");
            else if (isPersist() && isAddrTied()) // Global variable
                encoder.writeString(ATTRIB_CLASS, "global");
            else if (isConstant())
                encoder.writeString(ATTRIB_CLASS, "constant");
            else if (!isPersist() && (symbol != (Symbol*)0))
            {
                if (symbol->getCategory() == Symbol::function_parameter)
                    encoder.writeString(ATTRIB_CLASS, "param");
                else if (symbol->getScope()->isGlobal())
                    encoder.writeString(ATTRIB_CLASS, "global");
                else
                    encoder.writeString(ATTRIB_CLASS, "local");
            }
            else
            {
                encoder.writeString(ATTRIB_CLASS, "other");
            }
            if (isTypeLock())
                encoder.writeBool(ATTRIB_TYPELOCK, true);
            if (symbol != (Symbol*)0)
            {
                encoder.writeUnsignedInteger(ATTRIB_SYMREF, symbol->getId());
                if (symboloffset >= 0)
                    encoder.writeSignedInteger(ATTRIB_OFFSET, symboloffset);
            }
            getType()->encode(encoder);
            for (int4 j = 0; j < inst.size(); ++j)
            {
                encoder.openElement(ELEM_ADDR);
                encoder.writeUnsignedInteger(ATTRIB_REF, inst[j]->getCreateIndex());
                encoder.closeElement(ELEM_ADDR);
            }
            encoder.closeElement(ELEM_HIGH);
        }

#if MERGEMULTI_DEBUG
        /// \brief Check that there are no internal Cover intersections within \b this
        ///
        /// Look for any pair of Varnodes whose covers intersect, but they are not
        /// COPY shadows.  Throw an exception in this case.
        public void verifyCover()
        {
          Cover accumCover;

          for(int4 i=0;i<inst.size();++i) {
            Varnode *vn = inst[i];
            if (accumCover.intersect(*vn->getCover()) == 2) {
              for(int4 j=0;j<i;++j) {
	        Varnode *otherVn = inst[j];
	        if (otherVn->getCover()->intersect(*vn->getCover())==2) {
	          if (!otherVn->copyShadow(vn))
	            throw new LowlevelError("HighVariable has internal intersection");
	        }
              }
            }
            accumCover.merge(*vn->getCover());
          }
        }
#endif

        //  Varnode *findGlobalRep(void);
        /// Determine which given Varnode is most nameable
        /// Given two Varnode (members), sort them based on naming properties:
        ///  - A Varnode with an assigned name is preferred
        ///  - An \e unaffected Varnode is preferred
        ///  - A global Varnode is preferred
        ///  - An \e input Varnode is preferred
        ///  - An \e address \e tied Varnode is preferred
        ///  - A non-temporary Varnode is preferred
        ///  - A written Varnode is preferred
        ///  - An earlier Varnode is preferred
        ///
        /// \return \b true if the second Varnode's name would override the first's
        public static bool compareName(Varnode vn1, Varnode vn2)
        {
            if (vn1->isNameLock()) return false; // Check for namelocks
            if (vn2->isNameLock()) return true;

            if (vn1->isUnaffected() != vn2->isUnaffected()) // Prefer unaffected
                return vn2->isUnaffected();
            if (vn1->isPersist() != vn2->isPersist()) // Prefer persistent
                return vn2->isPersist();
            if (vn1->isInput() != vn2->isInput())   // Prefer an input
                return vn2->isInput();
            if (vn1->isAddrTied() != vn2->isAddrTied()) // Prefer address tied
                return vn2->isAddrTied();
            if (vn1->isProtoPartial() != vn2->isProtoPartial()) // Prefer pieces
                return vn2->isProtoPartial();

            // Prefer NOT internal
            if ((vn1->getSpace()->getType() != IPTR_INTERNAL) &&
                (vn2->getSpace()->getType() == IPTR_INTERNAL))
                return false;
            if ((vn1->getSpace()->getType() == IPTR_INTERNAL) &&
                (vn2->getSpace()->getType() != IPTR_INTERNAL))
                return true;
            if (vn1->isWritten() != vn2->isWritten()) // Prefer written
                return vn2->isWritten();
            if (!vn1->isWritten())
                return false;
            // Prefer earlier
            if (vn1->getDef()->getTime() != vn2->getDef()->getTime())
                return (vn2->getDef()->getTime() < vn1->getDef()->getTime());
            return false;
        }

        /// Compare based on storage location
        /// Compare two Varnode objects based just on their storage address
        /// \param a is the first Varnode to compare
        /// \param b is the second Varnode
        /// \return \b true if the first Varnode should be ordered before the second
        public static bool compareJustLoc(Varnode a, Varnode b) = >(a->getAddr() < b->getAddr());

        /// Mark and collect variables in expression
        /// Given a Varnode at the root of an expression, we collect all the \e explicit HighVariables
        /// involved in the expression.  This should only be run after \e explicit and \e implicit
        /// properties have been computed on Varnodes.  The expression is traced back from the root
        /// until explicit Varnodes are encountered; then their HighVariable is marked and added to the list.
        /// The routine returns a value based on PcodeOps encountered in the expression:
        ///   - 1 for call instructions
        ///   - 2 for LOAD instructions
        ///   - 3 for both call and LOAD
        ///   - 0 for no calls or LOADS
        ///
        /// \param vn is the given root Varnode of the expression
        /// \param highList will hold the collected HighVariables
        /// \return a value based on call and LOAD instructions in the expression
        public static int4 markExpression(Varnode vn, List<HighVariable> highList)
        {
            HighVariable* high = vn->getHigh();
            high->setMark();
            highList.push_back(high);
            int4 retVal = 0;
            if (!vn->isWritten()) return retVal;

            vector<PcodeOpNode> path;
            PcodeOp* op = vn->getDef();
            if (op->isCall())
                retVal |= 1;
            if (op->code() == CPUI_LOAD)
                retVal |= 2;
            path.push_back(PcodeOpNode(op, 0));
            while (!path.empty())
            {
                PcodeOpNode & node(path.back());
                if (node.op->numInput() <= node.slot)
                {
                    path.pop_back();
                    continue;
                }
                Varnode* curVn = node.op->getIn(node.slot);
                node.slot += 1;
                if (curVn->isAnnotation()) continue;
                if (curVn->isExplicit())
                {
                    high = curVn->getHigh();
                    if (high->isMark()) continue;   // Already in the list
                    high->setMark();
                    highList.push_back(high);
                    continue;               // Truncate at explicit
                }
                if (!curVn->isWritten()) continue;
                op = curVn->getDef();
                if (op->isCall())
                    retVal |= 1;
                if (op->code() == CPUI_LOAD)
                    retVal |= 2;
                path.push_back(PcodeOpNode(curVn->getDef(), 0));
            }
            return retVal;
        }
    }
}
