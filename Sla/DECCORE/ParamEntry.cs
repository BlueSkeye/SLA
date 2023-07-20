using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief A contiguous range of memory that can be used to pass parameters
    /// This range can be used to pass a single parameter (isExclusion() == \b true).  This
    /// is intended to model a parameter passed in a register.  The logical value does not
    /// have to fill the entire range.  The size in bytes can range from a minimum, getMinSize(),
    /// to the whole range, getSize(). Justification and extension of the logical value within
    /// the range can be specified.
    /// Alternately the range can be used as a resource for multiple parameters
    /// (isExclusion() == \b false).  In this case, the parameters are allocated sequentially
    /// (usually) starting from the front of the range.  The amount of space consumed by each
    /// parameter is dictated by an \e alignment setting in bytes.
    /// A ParamEntry can be associated with a particular class of data-types. Usually:
    ///   - TYPE_UNKNOWN   for general purpose parameters
    ///   - TYPE_FLOAT     for dedicated floating-point registers
    internal class ParamEntry
    {
        [Flags()]
        public enum ParamFlags
        {
            /// Big endian values are left justified within their slot
            force_left_justify = 1,
            /// Slots (for \e non-exlusion entries) are allocated in reverse order
            reverse_stack = 2,
            /// Assume values that are below the max \b size are zero extended into this container
            smallsize_zext = 4,
            /// Assume values that are below the max \b size are sign extended into this container
            smallsize_sext = 8,
            //    is_big_endian = 16,		///< Set if this value should be treated as big endian
            /// Assume values that are below the max \b size are sign OR zero extended based on integer type
            smallsize_inttype = 32,
            /// Assume values smaller than max \b size are floating-point extended to full size
            smallsize_floatext = 64,
            /// Perform extra checks during parameter recovery on most sig portion of the double
            extracheck_high = 128,
            /// Perform extra checks during parameter recovery on least sig portion of the double
            extracheck_low = 256,
            /// This entry is grouped with other entries
            is_grouped = 512,
            /// Overlaps an earlier entry (and doesn't consume additional resource slots)
            overlapping = 0x100
        }
        public enum Containment
        {
            no_containment,     ///< Range neither contains nor is contained by a ParamEntry
            contains_unjustified,   ///< ParamEntry contains range, but the range does not cover the least significant bytes
            contains_justified,     ///< ParamEntry contains range, which covers the least significant bytes
            contained_by        ///< ParamEntry is contained by the range
        }

        /// Boolean properties of the parameter
        private uint flags;
        /// Data-type class that this entry must match
        private type_metatype type;
        /// Group(s) \b this entry belongs to
        private List<int> groupSet;
        /// Address space containing the range
        private AddrSpace spaceid;
        /// Starting offset of the range
        private uintb addressbase;
        /// Size of the range in bytes
        private int size;
        /// Minimum bytes allowed for the logical value
        private int minsize;
        /// How much alignment (0 means only 1 logical value is allowed)
        private int alignment;
        /// (Maximum) number of slots that can store separate parameters
        private int numslots;
        /// Non-null if this is logical variable from joined pieces
        private JoinRecord joinrec;

        /// \brief Find a ParamEntry matching the given storage Varnode
        ///
        /// Search through the list backward.
        /// \param entryList is the list of ParamEntry to search through
        /// \param vn is the storage to search for
        /// \return the matching ParamEntry or null
        private static ParamEntry findEntryByStorage(List<ParamEntry> entryList, VarnodeData vn)
        {
            list<ParamEntry>::const_reverse_iterator iter = entryList.rbegin();
            for (; iter != entryList.rend(); ++iter)
            {
                const ParamEntry &entry(*iter);
                if (entry.spaceid == vn.space && entry.addressbase == vn.offset && entry.size == vn.size)
                {
                    return &entry;
                }
            }
            return (const ParamEntry*)0;
        }

        /// Make adjustments for a \e join ParamEntry
        /// If the ParamEntry is initialized with a \e join address, cache the join record and
        /// adjust the group and groupsize based on the ParamEntrys being overlapped
        /// \param curList is the current list of ParamEntry
        private void resolveJoin(List<ParamEntry> curList)
        {
            if (spaceid->getType() != IPTR_JOIN)
            {
                joinrec = (JoinRecord*)0;
                return;
            }
            joinrec = spaceid->getManager()->findJoin(addressbase);
            groupSet.clear();
            for (int4 i = 0; i < joinrec->numPieces(); ++i)
            {
                ParamEntry entry = findEntryByStorage(curList, joinrec->getPiece(i));
                if (entry != (ParamEntry*)0) {
                    groupSet.insert(groupSet.end(), entry->groupSet.begin(), entry->groupSet.end());
                    // For output <pentry>, if the most signifigant part overlaps with an earlier <pentry>
                    // the least signifigant part is marked for extra checks, and vice versa.
                    flags |= (i == 0) ? extracheck_low : extracheck_high;
                }
            }
            if (groupSet.empty())
                throw new LowlevelError("<pentry> join must overlap at least one previous entry");
            sort(groupSet.begin(), groupSet.end());
            flags |= overlapping;
        }

        /// Make adjustments for ParamEntry that overlaps others
        /// Search for overlaps of \b this with any previous entry.  If an overlap is discovered,
        /// verify the form is correct for the different ParamEntry to share \e group slots and
        /// reassign \b this group.
        /// \param curList is the list of previous entries
        private void resolveOverlap(List<ParamEntry> curList)
        {
            if (joinrec != (JoinRecord*)0)
                return;     // Overlaps with join records dealt with in resolveJoin
            vector<int4> overlapSet;
            list<ParamEntry>::const_iterator iter, enditer;
            Address addr(spaceid, addressbase);
            enditer = curList.end();
            --enditer;      // The last entry is \b this ParamEntry
            for (iter = curList.begin(); iter != enditer; ++iter)
            {
                const ParamEntry &entry(*iter);
                if (!entry.intersects(addr, size)) continue;
                if (contains(entry))
                {   // If this contains the intersecting entry
                    if (entry.isOverlap()) continue;    // Don't count resources (already counted overlapped entry)
                    overlapSet.insert(overlapSet.end(), entry.groupSet.begin(), entry.groupSet.end());
                    // For output <pentry>, if the most signifigant part overlaps with an earlier <pentry>
                    // the least signifigant part is marked for extra checks, and vice versa.
                    if (addressbase == entry.addressbase)
                        flags |= spaceid->isBigEndian() ? extracheck_low : extracheck_high;
                    else
                        flags |= spaceid->isBigEndian() ? extracheck_high : extracheck_low;
                }
                else
                    throw LowlevelError("Illegal overlap of <pentry> in compiler spec");
            }

            if (overlapSet.empty()) return;     // No overlaps
            sort(overlapSet.begin(), overlapSet.end());
            groupSet = overlapSet;
            flags |= overlapping;
        }

        /// \brief Is the logical value left-justified within its container
        private bool isLeftJustified()
            => (((flags&force_left_justify)!=0)||(!spaceid->isBigEndian()));

        /// Constructor for use with decode
        public ParamEntry(int grp)
        {
            groupSet.Add(grp);
        }

        /// Get the group id \b this belongs to
        public int getGroup() => groupSet[0];

        /// Get all group numbers \b this overlaps
        public List<int> getAllGroups() => groupSet;

        /// Check if \b this and op2 occupy any of the same groups
        /// \param op2 is the other entry to compare
        /// \return \b true if the group sets associated with each ParamEntry intersect at all
        public bool groupOverlap(ParamEntry op2)
        {
            int4 i = 0;
            int4 j = 0;
            int4 valThis = groupSet[i];
            int4 valOther = op2.groupSet[j];
            while (valThis != valOther)
            {
                if (valThis < valOther)
                {
                    i += 1;
                    if (i >= groupSet.size()) return false;
                    valThis = groupSet[i];
                }
                else
                {
                    j += 1;
                    if (j >= op2.groupSet.size()) return false;
                    valOther = op2.groupSet[j];
                }
            }
            return true;
        }

        /// Get the size of the memory range in bytes.
        public int getSize() => size;

        /// Get the minimum size of a logical value contained in \b this
        public int getMinSize() => minsize;

        /// Get the alignment of \b this entry
        public int getAlign() => alignment;

        /// Get record describing joined pieces (or null if only 1 piece)
        public JoinRecord getJoinRecord() => joinrec;

        /// Get the data-type class associated with \b this
        public type_metatype getType() => type;

        /// Return \b true if this holds a single parameter exclusively
        public bool isExclusion() => (alignment==0);

        /// Return \b true if parameters are allocated in reverse order
        public bool isReverseStack() => ((flags & reverse_stack)!= 0);

        /// Return \b true if \b this is grouped with other entries
        public bool isGrouped() => ((flags & is_grouped)!= 0);

        /// Return \b true if \b this overlaps another entry
        public bool isOverlap() => ((flags & overlapping)!= 0);

        /// Does \b this subsume the definition of the given ParamEntry
        /// This entry must properly contain the other memory range, and
        /// the entry properties must be compatible.  A \e join ParamEntry can
        /// subsume another \e join ParamEntry, but we expect the addressbase to be identical.
        /// \param op2 is the given entry to compare with \b this
        /// \return \b true if the given entry is subsumed
        public bool subsumesDefinition(ParamEntry op2)
        {
            if ((type != TYPE_UNKNOWN) && (op2.type != type)) return false;
            if (spaceid != op2.spaceid) return false;
            if (op2.addressbase < addressbase) return false;
            if ((op2.addressbase + op2.size - 1) > (addressbase + size - 1)) return false;
            if (alignment != op2.alignment) return false;
            return true;
        }

        /// Is this entry contained by the given range
        /// We assume a \e join ParamEntry cannot be contained by a single contiguous memory range.
        /// \param addr is the starting address of the potential containing range
        /// \param sz is the number of bytes in the range
        /// \return \b true if the entire ParamEntry fits inside the range
        public bool containedBy(Address addr, int sz)
        {
            if (spaceid != addr.getSpace()) return false;
            if (addressbase < addr.getOffset()) return false;
            uintb entryoff = addressbase + size - 1;
            uintb rangeoff = addr.getOffset() + sz - 1;
            return (entryoff <= rangeoff);
        }

        /// Does \b this intersect the given range in some way
        /// If \b this a a \e join, each piece is tested for intersection.
        /// Otherwise, \b this, considered as a single memory, is tested for intersection.
        /// \param addr is the starting address of the given memory range to test against
        /// \param sz is the number of bytes in the given memory range
        /// \return \b true if there is any kind of intersection
        public bool intersects(Address addr, int sz)
        {
            uintb rangeend;
            if (joinrec != (JoinRecord*)0)
            {
                rangeend = addr.getOffset() + sz - 1;
                for (int4 i = 0; i < joinrec->numPieces(); ++i)
                {
                    const VarnodeData &vdata(joinrec->getPiece(i));
                    if (addr.getSpace() != vdata.space) continue;
                    uintb vdataend = vdata.offset + vdata.size - 1;
                    if (addr.getOffset() < vdata.offset && rangeend < vdataend)
                        continue;
                    if (addr.getOffset() > vdata.offset && rangeend > vdataend)
                        continue;
                    return true;
                }
            }
            if (spaceid != addr.getSpace()) return false;
            rangeend = addr.getOffset() + sz - 1;
            uintb thisend = addressbase + size - 1;
            if (addr.getOffset() < addressbase && rangeend < thisend)
                return false;
            if (addr.getOffset() > addressbase && rangeend > thisend)
                return false;
            return true;
        }

        /// Calculate endian aware containment
        /// Check if the given memory range is contained in \b this.
        /// If it is contained, return the endian aware offset of the containment.
        /// I.e. if the least significant byte of the given range falls on the least significant
        /// byte of the \b this, return 0.  If it intersects the second least significant, return 1, etc.
        /// \param addr is the starting address of the given memory range
        /// \param sz is the size of the given memory range in bytes
        /// \return the endian aware alignment or -1 if the given range isn't contained
        public int justifiedContain(Address addr, int sz)
        {
            if (joinrec != (JoinRecord*)0)
            {
                int4 res = 0;
                for (int4 i = joinrec->numPieces() - 1; i >= 0; --i)
                { // Move from least significant to most
                    const VarnodeData &vdata(joinrec->getPiece(i));
                    int4 cur = vdata.getAddr().justifiedContain(vdata.size, addr, sz, false);
                    if (cur < 0)
                        res += vdata.size;  // We skipped this many less significant bytes
                    else
                    {
                        return res + cur;
                    }
                }
                return -1;          // Not contained at all
            }
            if (alignment == 0)
            {
                // Ordinary endian containment
                Address entry(spaceid, addressbase);
                return entry.justifiedContain(size, addr, sz, ((flags & force_left_justify) != 0));
            }
            if (spaceid != addr.getSpace()) return -1;
            uintb startaddr = addr.getOffset();
            if (startaddr < addressbase) return -1;
            uintb endaddr = startaddr + sz - 1;
            if (endaddr < startaddr) return -1; // Don't allow wrap around
            if (endaddr > (addressbase + size - 1)) return -1;
            startaddr -= addressbase;
            endaddr -= addressbase;
            if (!isLeftJustified())
            {   // For right justified (big endian), endaddr must be aligned
                int4 res = (int4)((endaddr + 1) % alignment);
                if (res == 0) return 0;
                return (alignment - res);
            }
            return (int4)(startaddr % alignment);
        }

        /// \brief Calculate the containing memory range
        ///
        /// Pass back the VarnodeData (space,offset,size) of the parameter that would contain
        /// the given memory range.  If \b this contains the range and is \e exclusive, just
        /// pass back \b this memory range.  Otherwise the passed back range will depend on
        /// alignment.
        /// \param addr is the starting address of the given range
        /// \param sz is the size of the given range in bytes
        /// \param res is the reference to VarnodeData that will be passed back
        /// \return \b true if the given range is contained at all
        public bool getContainer(Address addr, int sz, VarnodeData res)
        {
            Address endaddr = addr + (sz - 1);
            if (joinrec != (JoinRecord*)0)
            {
                for (int4 i = joinrec->numPieces() - 1; i >= 0; --i)
                { // Move from least significant to most
                    const VarnodeData &vdata(joinrec->getPiece(i));
                    if ((addr.overlap(0, vdata.getAddr(), vdata.size) >= 0) &&
                    (endaddr.overlap(0, vdata.getAddr(), vdata.size) >= 0))
                    {
                        res = vdata;
                        return true;
                    }
                }
                return false;       // Not contained at all
            }
            Address entry(spaceid, addressbase);
            if (addr.overlap(0, entry, size) < 0) return false;
            if (endaddr.overlap(0, entry, size) < 0) return false;
            if (alignment == 0)
            {
                // Ordinary endian containment
                res.space = spaceid;
                res.offset = addressbase;
                res.size = size;
                return true;
            }
            uintb al = (addr.getOffset() - addressbase) % alignment;
            res.space = spaceid;
            res.offset = addr.getOffset() - al;
            res.size = (int4)(endaddr.getOffset() - res.offset) + 1;
            int4 al2 = res.size % alignment;
            if (al2 != 0)
                res.size += (alignment - al2); // Bump up size to nearest alignment
            return true;
        }

        /// Does \b this contain the given entry (as a subpiece)
        /// Test that \b this, as one or more memory ranges, contains the other ParamEntry's memory range.
        /// A \e join ParamEntry cannot be contained by another entry, but it can contain an entry in one
        /// of its pieces.
        /// \param op2 is the given ParamEntry to test for containment
        /// \return \b true if the given ParamEntry is contained
        public bool contains(ParamEntry op2)
        {
            if (op2.joinrec != (JoinRecord*)0) return false;    // Assume a join entry cannot be contained
            if (joinrec == (JoinRecord*)0)
            {
                Address addr(spaceid, addressbase);
                return op2.containedBy(addr, size);
            }
            for (int4 i = 0; i < joinrec->numPieces(); ++i)
            {
                const VarnodeData &vdata(joinrec->getPiece(i));
                Address addr = vdata.getAddr();
                if (op2.containedBy(addr, vdata.size))
                    return true;
            }
            return false;
        }

        /// \brief Calculate the type of \e extension to expect for the given logical value
        ///
        /// Return:
        ///   - CPUI_COPY if no extensions are assumed for small values in this container
        ///   - CPUI_INT_SEXT indicates a sign extension
        ///   - CPUI_INT_ZEXT indicates a zero extension
        ///   - CPUI_PIECE indicates an integer extension based on type of parameter
        ///
        ///  (A CPUI_FLOAT2FLOAT=float extension is handled by heritage and JoinRecord)
        /// If returning an extension operator, pass back the container being extended.
        /// \param addr is the starting address of the logical value
        /// \param sz is the size of the logical value in bytes
        /// \param res will hold the passed back containing range
        /// \return the type of extension
        public OpCode assumedExtension(Address addr, int sz, VarnodeData res)
        {
            if ((flags & (smallsize_zext | smallsize_sext | smallsize_inttype)) == 0) return CPUI_COPY;
            if (alignment != 0)
            {
                if (sz >= alignment)
                    return CPUI_COPY;
            }
            else if (sz >= size)
                return CPUI_COPY;
            if (joinrec != (JoinRecord*)0) return CPUI_COPY;
            if (justifiedContain(addr, sz) != 0) return CPUI_COPY; // (addr,sz) is not justified properly to allow an extension
            if (alignment == 0)
            {   // If exclusion, take up the whole entry
                res.space = spaceid;
                res.offset = addressbase;
                res.size = size;
            }
            else
            {   // Otherwise take up whole alignment
                res.space = spaceid;
                int4 alignAdjust = (addr.getOffset() - addressbase) % alignment;
                res.offset = addr.getOffset() - alignAdjust;
                res.size = alignment;
            }
            if ((flags & smallsize_zext) != 0)
                return CPUI_INT_ZEXT;
            if ((flags & smallsize_inttype) != 0)
                return CPUI_PIECE;
            return CPUI_INT_SEXT;
        }

        /// \brief Calculate the \e slot occupied by a specific address
        ///
        /// For \e non-exclusive entries, the memory range can be divided up into
        /// \b slots, which are chunks that take up a full alignment. I.e. for an entry with
        /// alignment 4, slot 0 is bytes 0-3 of the range, slot 1 is bytes 4-7, etc.
        /// Assuming the given address is contained in \b this entry, and we \b skip ahead a number of bytes,
        /// return the \e slot associated with that byte.
        /// NOTE: its important that the given address has already been checked for containment.
        /// \param addr is the given address
        /// \param skip is the number of bytes to skip ahead
        /// \return the slot index
        public int getSlot(Address addr, int skip)
        {
            int4 res = groupSet[0];
            if (alignment != 0)
            {
                uintb diff = addr.getOffset() + skip - addressbase;
                int4 baseslot = (int4)diff / alignment;
                if (isReverseStack())
                    res += (numslots - 1) - baseslot;
                else
                    res += baseslot;
            }
            else if (skip != 0)
            {
                res = groupSet.back();
            }
            return res;
        }

        /// Get the address space containing \b this entry
        public AddrSpace getSpace() => spaceid;

        /// Get the starting offset of \b this entry
        public uintb getBase() => addressbase;

        /// \brief Calculate the storage address assigned when allocating a parameter of a given size
        ///
        /// Assume \b slotnum slots have already been assigned and increment \b slotnum
        /// by the number of slots used.
        /// Return an invalid address if the size is too small or if there are not enough slots left.
        /// \param slotnum is a reference to used slots (which will be updated)
        /// \param sz is the size of the parameter to allocated
        /// \return the address of the new parameter (or an invalid address)
        public Address getAddrBySlot(int slot, int sz)
        {
            Address res;            // Start with an invalid result
            int4 spaceused;
            if (sz < minsize) return res;
            if (alignment == 0)
            {       // If not an aligned entry (allowing multiple slots)
                if (slotnum != 0) return res; // Can only allocate slot 0
                if (sz > size) return res;  // Check on maximum size
                res = Address(spaceid, addressbase);    // Get base address of the slot
                spaceused = size;
                if (((flags & smallsize_floatext) != 0) && (sz != size))
                { // Do we have an implied floating-point extension
                    AddrSpaceManager* manager = spaceid->getManager();
                    res = manager->constructFloatExtensionAddress(res, size, sz);
                    return res;
                }
            }
            else
            {
                int4 slotsused = sz / alignment; // How many slots does a -sz- byte object need
                if ((sz % alignment) != 0)
                    slotsused += 1;
                if (slotnum + slotsused > numslots) // Check if there are enough slots left
                    return res;
                spaceused = slotsused * alignment;
                int4 index;
                if (isReverseStack())
                {
                    index = numslots;
                    index -= slotnum;
                    index -= slotsused;
                }
                else
                    index = slotnum;
                res = Address(spaceid, addressbase + index * alignment);
                slotnum += slotsused;   // Inform caller of number of slots used
            }
            if (!isLeftJustified())   // Adjust for right justified (big endian)
                res = res + (spaceused - sz);
            return res;
        }

        /// \brief Decode a \<pentry> element into \b this object
        ///
        /// \param decoder is the stream decoder
        /// \param normalstack is \b true if the parameters should be allocated from the front of the range
        /// \param grouped is \b true if \b this will be grouped with other entries
        /// \param curList is the list of ParamEntry defined up to this point
        public void decode(Decoder decoder, bool normalstack, bool grouped, List<ParamEntry> curList)
        {
            flags = 0;
            type = TYPE_UNKNOWN;
            size = minsize = -1;        // Must be filled in
            alignment = 0;      // default
            numslots = 1;

            uint4 elemId = decoder.openElement(ELEM_PENTRY);
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_MINSIZE)
                {
                    minsize = decoder.readSignedInteger();
                }
                else if (attribId == ATTRIB_SIZE)
                { // old style
                    alignment = decoder.readSignedInteger();
                }
                else if (attribId == ATTRIB_ALIGN)
                { // new style
                    alignment = decoder.readSignedInteger();
                }
                else if (attribId == ATTRIB_MAXSIZE)
                {
                    size = decoder.readSignedInteger();
                }
                else if (attribId == ATTRIB_METATYPE)
                    type = string2metatype(decoder.readString());
                else if (attribId == ATTRIB_EXTENSION)
                {
                    flags &= ~((uint4)(smallsize_zext | smallsize_sext | smallsize_inttype));
                    string ext = decoder.readString();
                    if (ext == "sign")
                        flags |= smallsize_sext;
                    else if (ext == "zero")
                        flags |= smallsize_zext;
                    else if (ext == "inttype")
                        flags |= smallsize_inttype;
                    else if (ext == "float")
                        flags |= smallsize_floatext;
                    else if (ext != "none")
                        throw LowlevelError("Bad extension attribute");
                }
                else
                    throw LowlevelError("Unknown <pentry> attribute");
            }
            if ((size == -1) || (minsize == -1))
                throw LowlevelError("ParamEntry not fully specified");
            if (alignment == size)
                alignment = 0;
            Address addr;
            addr = Address::decode(decoder);
            decoder.closeElement(elemId);
            spaceid = addr.getSpace();
            addressbase = addr.getOffset();
            if (alignment != 0)
            {
                //    if ((addressbase % alignment) != 0)
                //      throw LowlevelError("Stack <pentry> address must match alignment");
                numslots = size / alignment;
            }
            if (spaceid->isReverseJustified())
            {
                if (spaceid->isBigEndian())
                    flags |= force_left_justify;
                else
                    throw LowlevelError("No support for right justification in little endian encoding");
            }
            if (!normalstack)
            {
                flags |= reverse_stack;
                if (alignment != 0)
                {
                    if ((size % alignment) != 0)
                        throw LowlevelError("For positive stack growth, <pentry> size must match alignment");
                }
            }
            if (grouped)
                flags |= is_grouped;
            resolveJoin(curList);
            resolveOverlap(curList);
        }

        /// Return \b true if there is a high overlap
        public bool isParamCheckHigh() => ((flags & extracheck_high)!= 0);

        /// Return \b true if there is a low overlap
        public bool isParamCheckLow() => ((flags & extracheck_low)!= 0);

        /// Enforce ParamEntry group ordering rules
        /// Entries within a group must be distinguishable by size or by type.
        /// Throw an exception if the entries aren't distinguishable
        /// \param entry1 is the first ParamEntry to compare
        /// \param entry2 is the second ParamEntry to compare
        public static void orderWithinGroup(ParamEntry entry1, ParamEntry entry2)
        {
            if (entry2.minsize > entry1.size || entry1.minsize > entry2.size)
                return;
            if (entry1.type != entry2.type)
            {
                if (entry1.type == TYPE_UNKNOWN)
                {
                    throw LowlevelError("<pentry> tags with a specific type must come before the general type");
                }
                return;
            }
            throw LowlevelError("<pentry> tags within a group must be distinguished by size or type");
        }
    }
}
