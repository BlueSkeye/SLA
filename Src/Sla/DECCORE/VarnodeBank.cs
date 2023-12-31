﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A container for Varnode objects from a specific function
    ///
    /// The API allows the creation, deletion, search, and iteration of
    /// Varnode objects from one function.  The class maintains two ordering
    /// for efficiency:
    ///    - Sorting based on storage location (\b loc)
    ///    - Sorting based on point of definition (\b def)
    /// The class maintains a \e last \e offset counter for allocation
    /// temporary Varnode objects in the \e unique space. Constants are created
    /// by passing a constant address to the create() method.
    internal class VarnodeBank
    {
        /// Underlying address space manager
        private AddrSpaceManager manage;
        /// Space to allocate unique varnodes from
        private AddrSpace uniq_space;
        /// Base for unique addresses
        private uint uniqbase;
        /// Counter for generating unique offsets
        private uint uniqid;
        /// Number of varnodes created
        private uint create_index;
        /// Varnodes sorted by location then def
        private VarnodeLocSet loc_tree = new VarnodeLocSet();
        /// Varnodes sorted by def then location
        private VarnodeDefSet def_tree = new VarnodeDefSet();
        /// Template varnode for searching trees
        private /*mutable*/ Varnode searchvn;

        /// Insert a Varnode into the sorted lists
        /// Enter the Varnode into both the \e location and \e definition based trees.
        /// Update the Varnode iterators and flags
        /// \param vn is the Varnode object to insert
        /// \return the inserted object, which may not be the same as the input Varnode
        private Varnode xref(Varnode vn)
        {
            Tuple<IEnumerator<Varnode>, bool> check;
            Varnode othervn;
            Varnode? actualNode;

            check = loc_tree.Add(vn);
            if (loc_tree.TryGetValue(vn, out actualNode)) {
                // Set already contains this varnode
                othervn = actualNode ?? throw new ApplicationException();
                replace(vn, othervn); // Patch ops using the old varnode
                // delete vn;
                return othervn;
            }
            // Otherwise a new insertion
            loc_tree.Add(vn);
            vn.lociter = check.Item1;
            vn.setFlags(Varnode.varnode_flags.insert);
            // Insertion should also be new in def_tree
            vn.defiter = def_tree.Add(vn).first;
            return vn;
        }

        /// Construct the container
        /// \param m is the underlying address space manager
        public VarnodeBank(AddrSpaceManager m)
        {
            searchvn = new Varnode(0, new Address(Address.mach_extreme.m_minimal), (Datatype)null);
            manage = m;
            searchvn.flags = Varnode.varnode_flags.input; // searchvn is always an input varnode of size 0
            uniq_space = m.getUniqueSpace();
            uniqbase = uniq_space.getTrans().getUniqueStart(Translate.UniqueLayout.ANALYSIS);
            uniqid = uniqbase;
            create_index = 0;
        }

        /// Clear out all Varnodes and reset counters
        public void clear()
        {
            //VarnodeLocSet::iterator iter;

            //for (iter = loc_tree.begin(); iter != loc_tree.end(); ++iter)
            //    delete* iter;

            loc_tree.clear();
            def_tree.clear();
            uniqid = uniqbase;      // Reset counter to base value
            create_index = 0;       // Reset varnode creation index
        }

        ~VarnodeBank()
        {
            clear();
        }

        /// Get number of Varnodes \b this contains
        public int numVarnodes() => loc_tree.size();

        /// Create a \e free Varnode object
        /// The Varnode is created and inserted into the maps as \e free: not
        /// defined as the output of a p-code op or the input to a function.
        /// \param s is the size of the Varnode in bytes
        /// \param m is the starting address
        /// \param ct is the data-type of the new varnode (must not be NULL)
        /// \return the newly allocated Varnode object
        public Varnode create(int s, Address m, Datatype ct)
        {
            Varnode vn = new Varnode(s, m, ct) {
                create_index = create_index++,
            };
            // Frees can always be inserted without duplication
            vn.lociter = loc_tree.insert(vn).first;
            vn.defiter = def_tree.insert(vn).first;
            return vn;
        }

        /// Create a Varnode as the output of a PcodeOp
        /// The new Varnode object will already be put in the \e definition list as if
        /// it were the output of the given PcodeOp. The Varnode must still be set as the output.
        /// \param s is the size in bytes
        /// \param m is the starting address
        /// \param ct is the data-type to associate
        /// \param op is the given PcodeOp
        public Varnode createDef(int s, Address m, Datatype ct, PcodeOp op)
        {
            Varnode vn = new Varnode(s, m, ct) {
                create_index = create_index + 1
            };
            vn.setDef(op);
            return xref(vn);
        }

        /// Create a temporary varnode
        /// The Varnode is allocated in the \e unique space and automatically
        /// assigned an offset.  The Varnode is initially \e free.
        /// \param s is the size of the Varnode in bytes
        /// \param ct is the data-type to assign (must not be NULL)
        public Varnode createUnique(int s, Datatype ct)
        {
            // Generate a unique address
            Address addr = new Address(uniq_space, uniqid);
            // Update counter for next call
            uniqid += (uint)s;
            // Build varnode with our generated address
            return create(s, addr, ct);
        }

        /// Create a temporary Varnode as output of a PcodeOp
        /// The new Varnode will be assigned from the \e unique space, and
        /// it will already be put in the \e definition list as if
        /// it were the output of the given PcodeOp. The Varnode must still be set as the output.
        /// \param s is the size in bytes
        /// \param ct is the data-type to associate
        /// \param op is the given PcodeOp
        public Varnode createDefUnique(int s, Datatype ct, PcodeOp op)
        {
            // Create unique varnode as output of op
            Address addr = new Address(uniq_space, uniqid);
            uniqid += (uint)s;
            return createDef(s, addr, ct, op);
        }

        /// Remove a Varnode from the container
        /// The Varnode object is removed from the sorted lists and
        /// its memory reclaimed
        /// \param vn is the Varnode to remove
        public void destroy(Varnode vn)
        {
            if ((vn.getDef() != (PcodeOp)null) || (!vn.hasNoDescend()))
                throw new LowlevelError("Deleting integrated varnode");

            loc_tree.Remove(vn);
            def_tree.Remove(vn);
            // delete vn;
        }

        /// Mark a Varnode as an input to the function
        /// Define the Varnode as an input formally; it is no longer considered \e free.
        /// Its position in the cross-referencing lists will change
        /// \param vn is the Varnode to mark
        /// \return the modified Varnode, which be a different object than the original
        public Varnode setInput(Varnode vn)
        {
            if (!vn.isFree())
                throw new LowlevelError("Making input out of varnode which is not free");
            if (vn.isConstant())
                throw new LowlevelError("Making input out of constant varnode");

            // Erase the free version of varnode
            loc_tree.Remove(vn);
            def_tree.Remove(vn);

            vn.setInput();     // Set the input flag
            return xref(vn);
        }

        /// Change Varnode to be defined by the given PcodeOp
        /// The Varnode must initially be \e free. It will be removed
        /// from the cross-referencing lists and reinserted as if its were
        /// the output of the given PcodeOp.  It still must be explicitly set
        /// as the output.
        /// \param vn is the Varnode to modify
        /// \param op is the given PcodeOp
        /// \return the modified Varnode, which may be a different object than the original
        public Varnode setDef(Varnode vn, PcodeOp op)
        {
            if (!vn.isFree()) {
                TextWriter s = new StringWriter();
                Address addr = op.getAddr();
                s.Write($"Defining varnode which is not free at {addr.getShortcut()}");
                addr.printRaw(s);
                throw new LowlevelError(s.ToString());
            }
            if (vn.isConstant()) {
                TextWriter s = new StringWriter();
                Address addr = op.getAddr();
                s.Write($"Assignment to constant at {addr.getShortcut()}");
                addr.printRaw(s);
                throw new LowlevelError(s.ToString());
            }
            loc_tree.Remove(vn);
            def_tree.Remove(vn);
            vn.setDef(op);     // Change the varnode to be defined
            return xref(vn);
        }

        /// Convert a Varnode to be \e free
        /// The Varnode is removed from the cross-referencing lists and reinserted as
        /// as if it were not defined by any PcodeOp and not an input to the function.
        /// If the Varnode was originally a PcodeOp output, this must be explicitly cleared.
        /// \param vn is the Varnode to modify
        public void makeFree(Varnode vn)
        {
            loc_tree.Remove(vn);
            def_tree.Remove(vn);

            // Clear things that make vn non-free
            vn.setDef((PcodeOp)null);
            vn.clearFlags(Varnode.varnode_flags.insert | Varnode.varnode_flags.input | Varnode.varnode_flags.indirect_creation);

            // Re-insert as free varnode
            vn.lociter = loc_tree.Add(vn);
            vn.defiter = def_tree.Add(vn);
        }

        /// Replace every read of one Varnode with another
        /// Any PcodeOps that read \b oldvn are changed to read \b newvn
        /// \param oldvn is the old Varnode
        /// \param newvn is the Varnode to replace it with
        public void replace(Varnode oldvn, Varnode newvn)
        {
            List<PcodeOp> descending = oldvn.descend;
            for (int index = 0; index < descending.Count; index++) {
                PcodeOp op = descending[index];
                // Cannot be input to your own definition
                if (op.output == newvn) continue;
                int i = op.getSlot(oldvn);
                // Sever the link fully
                descending.Remove(op);
                // Before attempting to build the new link
                op.clearInput(i);
                newvn.addDescend(op);
                // This must be called AFTER descend is updated
                op.setInput(newvn, i);
            }
            oldvn.setFlags(Varnode.varnode_flags.coverdirty);
            newvn.setFlags(Varnode.varnode_flags.coverdirty);
        }

        /// Find a Varnode
        /// Find a Varnode given its (loc,size) and the address where it is defined.
        /// \param s is the size of the Varnode
        /// \param loc is its starting address
        /// \param pc is the address where it is defined
        /// \param uniq is the sequence number or -1 if not specified
        /// \return the matching Varnode or NULL
        public Varnode? find(int s, Address loc, Address pc, uint uniq = uint.MaxValue)
        {
            IEnumerator<Varnode> iter = beginLoc(s, loc, pc, uniq);
            while (iter.MoveNext()) {
                Varnode vn = iter.Current;
                if (vn.getSize() != s) break;
                if (vn.getAddr() != loc) break;
                PcodeOp? op = vn.getDef();
                if ((op != (PcodeOp)null) && (op.getAddr() == pc)) {
                    if ((uniq == uint.MaxValue) || (op.getTime() == uniq)) return vn;
                }
            }
            return (Varnode)null;
        }

        /// Find an input Varnode
        /// Find a Varnode marked as a function input given its size and address
        /// \param s is the size
        /// \param loc is the starting address
        /// \return the match Varnode object or NULL
        public Varnode? findInput(int s, Address loc)
        {
            IEnumerator<Varnode> iter = beginLoc(s, loc, Varnode.varnode_flags.input);
            if (iter.MoveNext()) {
                // There is only one possible varnode matching this
                Varnode vn = iter.Current;
                if (vn.isInput() && (vn.getSize() == s) && (vn.getAddr() == loc))
                    return vn;
            }
            return (Varnode)null;
        }

        /// Find an input Varnode contained within this range
        /// Find the first Varnode completely contained within the given range, which is
        /// also marked as a function input.
        /// \param s is the size of the range
        /// \param loc is the starting address of the range
        /// \return the Varnode object or NULL if no Varnode met the conditions
        public Varnode? findCoveredInput(int s, Address loc)
        {
            IEnumerator<Varnode> enditer;
            ulong highest = loc.getSpace().getHighest();
            ulong end = loc.getOffset() + (uint)s - 1;

            IEnumerator<Varnode> iter = beginDef(Varnode.varnode_flags.input, loc);
            if (end == highest) {
                // Check for wrap around of address
                Address tmp = new Address(loc.getSpace(), highest);
                enditer = endDef(Varnode.varnode_flags.input, tmp);
            }
            else {
                enditer = beginDef(Varnode.varnode_flags.input, loc + s);
            }

            while (iter.MoveNext()) {
                // we know vn is input with vn.Loc in (loc,loc+s)
                Varnode vn = iter.Current;
                if (vn.getOffset() + (uint)vn.getSize() - 1 <= end)
                    // vn is completely contained
                    return vn;
            }
            return (Varnode)null;
        }

        /// Find an input Varnode covering a range
        /// Search for the Varnode that completely contains the given range and is marked
        /// as an input to the function. If it exists, it is unique.
        /// \param s is the size of the range
        /// \param loc is the starting address of the range
        public Varnode? findCoveringInput(int s, Address loc)
        {
            IEnumerator<Varnode> iter = beginDef(Varnode.varnode_flags.input, loc);
            if (iter != def_tree.end()) {
                Varnode vn = iter.Current;
                if ((vn.getAddr() != loc) && (iter != def_tree.begin())) {
                    --iter;
                    vn = iter.Current;
                }
                if (vn.isInput() && (vn.getSpace() == loc.getSpace()) &&
                (vn.getOffset() <= loc.getOffset()) &&
                (vn.getOffset() + (uint)vn.getSize() - 1 >= loc.getOffset() + (uint)s - 1))
                    return vn;
            }
            return (Varnode)null;
        }

        /// Get the next creation index to be assigned
        public uint getCreateIndex() => create_index;

        /// Beginning of location list
        public IEnumerator<Varnode> beginLoc() => loc_tree.GetEnumerator();

        public IEnumerator<Varnode> beginLoc(Varnode startsWith)
            => loc_tree.GetViewBetween(startsWith, null).GetEnumerator();

        public IEnumerator<Varnode> beginReverseLoc() => loc_tree.Reverse().GetEnumerator();

        public IEnumerator<Varnode> beginReverseLoc(Varnode startsWith)
            => loc_tree.GetViewBetween(startsWith, null).Reverse().GetEnumerator();

        ///// End of location list
        //public IEnumerator<Varnode> endLoc() => loc_tree.end();

        /// \brief Beginning of Varnodes in given address space sorted by location
        ///
        /// \param spaceid is the given address space
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginLoc(AddrSpace spaceid)
        {
            searchvn.loc = new Address(spaceid, 0);
            return loc_tree.lower_bound(searchvn);
        }

        /// \brief Ending of Varnodes in given address space sorted by location
        ///
        /// \param spaceid is the given address space
        /// \return the ending iterator
        public IEnumerator<Varnode> endLoc(AddrSpace spaceid)
        {
            searchvn.loc = new Address(manage.getNextSpaceInOrder(spaceid), 0);
            return loc_tree.lower_bound(searchvn);
        }

        /// \brief Beginning of Varnodes starting at a given address sorted by location
        ///
        /// \param addr is the given starting address
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginLoc(Address addr)
        {
            searchvn.loc = addr;
            return loc_tree.lower_bound(searchvn);
        }

        /// \brief End of Varnodes starting at a given address sorted by location
        ///
        /// \param addr is the given starting address
        /// \return the ending iterator
        public IEnumerator<Varnode> endLoc(Address addr)
        {
            if (addr.getOffset() == addr.getSpace().getHighest()) {
                AddrSpace space = addr.getSpace();
                searchvn.loc = new Address(manage.getNextSpaceInOrder(space), 0);
            }
            else
                searchvn.loc = addr + 1;
            return loc_tree.lower_bound(searchvn);
        }

        /// \brief Beginning of Varnodes of given size and starting address sorted by location
        /// \param s is the given size
        /// \param addr is the given starting address
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginLoc(int s, Address addr)
        {
            searchvn.size = s;
            searchvn.loc = addr;
            IEnumerator<Varnode> iter = loc_tree.GetViewBetween(searchvn, searchvn).GetEnumerator();
            // IEnumerator<Varnode> iter = loc_tree.lower_bound(searchvn);
            // Return size to 0
            searchvn.size = 0;
            return iter;
        }

        /// \brief End of Varnodes of given size and starting address sorted by location
        /// \param s is the given size
        /// \param addr is the given starting address
        /// \return the ending iterator
        public IEnumerator<Varnode> endLoc(int s, Address addr)
        {
            searchvn.size = s + 1;
            searchvn.loc = addr;
            IEnumerator<Varnode> iter = loc_tree.lower_bound(searchvn);
            searchvn.size = 0;      // Return size to 0
            return iter;
        }

        /// \brief Beginning of Varnodes sorted by location
        ///
        /// Varnodes are restricted by a given size and location and by the property
        ///    - Varnode.varnode_flags.input for Varnodes that are inputs to the function
        ///    - Varnode.varnode_flags.written for Varnodes that are defined by a PcodeOp
        ///    - 0 for \e free Varnodes
        /// \param s is the given size
        /// \param addr is the given starting address
        /// \param fl is the property restriction
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginLoc(int s, Address addr, Varnode.varnode_flags fl)
        {
            IEnumerator<Varnode> iter;
            SeqNum sq;
            PcodeOp searchop;

            switch (fl) {
                case Varnode.varnode_flags.input:
                    searchvn.size = s;
                    searchvn.loc = addr;
                    iter = loc_tree.lower_bound(searchvn);
                    searchvn.size = 0;
                    return iter;
                case Varnode.varnode_flags.written:
                    sq = new SeqNum(Address.mach_extreme.m_minimal); // Minimal sequence number
                    searchop = new PcodeOp(0, sq);
                    searchvn.size = s;
                    searchvn.loc = addr;
                    searchvn.flags = Varnode.varnode_flags.written;
                    searchvn.def = searchop;
                    iter = loc_tree.lower_bound(searchvn);
                    searchvn.size = 0;
                    searchvn.flags = Varnode.varnode_flags.input;
                    return iter;
                default:
                    sq = new SeqNum(Address.mach_extreme.m_maximal); // Maximal sequence number
                    searchop = new PcodeOp(0, sq);
                    searchvn.size = s;
                    searchvn.loc = addr;
                    searchvn.flags = Varnode.varnode_flags.written;
                    searchvn.def = searchop;
                    iter = loc_tree.upper_bound(searchvn);
                    searchvn.size = 0;
                    searchvn.flags = Varnode.varnode_flags.input;
                    return iter;
            }
        }

        /// \brief End of Varnodes sorted by location
        ///
        /// Varnodes are restricted by a given size and location and by the property
        ///    - Varnode.varnode_flags.input for Varnodes that are inputs to the function
        ///    - Varnode.varnode_flags.written for Varnodes that are defined by a PcodeOp
        ///    - 0 for \e free Varnodes
        /// \param s is the given size
        /// \param addr is the given starting address
        /// \param fl is the property restriction
        /// \return the ending iterator
        public IEnumerator<Varnode> endLoc(int s,Address addr, Varnode.varnode_flags fl)
        {
            IEnumerator<Varnode> iter;
            searchvn.loc = addr;

            switch (fl)
            {
                case Varnode.varnode_flags.written:
                    searchvn.size = s;
                    searchvn.flags = Varnode.varnode_flags.written;
                    SeqNum sq = new SeqNum(Address.mach_extreme.m_maximal); // Maximal sequence number
                    PcodeOp searchop = new PcodeOp(0, sq);
                    searchvn.def = searchop;
                    iter = loc_tree.upper_bound(searchvn);
                    searchvn.size = 0;
                    searchvn.flags = Varnode.varnode_flags.input;
                    return iter;
                case Varnode.varnode_flags.input:
                    searchvn.size = s;
                    iter = loc_tree.upper_bound(searchvn);
                    searchvn.size = 0;
                    return iter;
                default:
                    searchvn.size = s + 1;
                    iter = loc_tree.lower_bound(searchvn); // Find following input varnode
                    searchvn.size = 0;
                    return iter;
            }
        }

        /// \brief Beginning of Varnodes sorted by location
        ///
        /// Varnodes are restricted by a given size and location and by the
        /// sequence number of the PcodeOp defining it
        /// \param s is the given size
        /// \param addr is the given starting address
        /// \param pc is the address of the PcodeOp defining the Varnode
        /// \param uniq is the sequence number of the PcodeOp or -1 for now sequence number restriction
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginLoc(int s, Address addr, Address pc, uint uniq)
        {
            // Find first varnode of given loc and size
            // defined at a particular location
            searchvn.size = s;
            searchvn.loc = addr;
            searchvn.flags = Varnode.varnode_flags.written;
            if (uniq == uint.MaxValue)    // If don't care about uniq
                uniq = 0;           // find earliest
            SeqNum sq = new SeqNum(pc, uniq);
            PcodeOp searchop = new PcodeOp(0,sq);
            searchvn.def = searchop;
            IEnumerator<Varnode> iter = loc_tree.lower_bound(searchvn);

            searchvn.size = 0;
            searchvn.flags = Varnode.varnode_flags.input;
            return iter;
        }

        /// \brief End of Varnodes sorted by location
        ///
        /// Varnodes are restricted by a given size and location and by the
        /// sequence number of the PcodeOp defining it
        /// \param s is the given size
        /// \param addr is the given starting address
        /// \param pc is the address of the PcodeOp defining the Varnode
        /// \param uniq is the sequence number of the PcodeOp or -1 for now sequence number restriction
        /// \return the ending iterator
        public IEnumerator<Varnode> endLoc(int s, Address addr, Address pc, uint uniq)
        {
            IEnumerator<Varnode> iter;
            searchvn.size = s;
            searchvn.loc = addr;
            searchvn.flags = Varnode.varnode_flags.written;
            //  if (uniq==uint.MaxValue)
            //    uniq = 0;
            SeqNum sq = new SeqNum(pc, uniq);
            PcodeOp searchop = new PcodeOp(0,sq);
            searchvn.def = searchop;
            iter = loc_tree.upper_bound(searchvn);

            searchvn.size = 0;
            searchvn.flags = Varnode.varnode_flags.input;
            return iter;
        }

        /// \brief Given start, return maximal range of overlapping Varnodes
        ///
        /// Advance the iterator until no Varnodes after the iterator intersect any Varnodes
        /// from the initial Varnode through the current iterator.  The range is returned as pairs
        /// of iterators to subranges. One subrange for each set of Varnodes with the same size and starting address.
        /// A final iterator to the next Varnode after the overlapping set is also passed back.
        /// \param iter is an iterator to the given start Varnode
        /// \param bounds holds the array of iterator pairs passed back
        /// \return the union of Varnode flags across the range
        public Varnode.varnode_flags overlapLoc(IEnumerator<Varnode> iter,
            List<IEnumerator<Varnode>> bounds)
        {
            Varnode vn = iter.Current;
            AddrSpace spc = vn.getSpace();
            ulong off = vn.getOffset();
            ulong maxOff = off + (uint)(vn.getSize() - 1);
            Varnode.varnode_flags flags = vn.getFlags();
            bounds.Add(iter);
            iter = endLoc(vn.getSize(), vn.getAddr(), Varnode.varnode_flags.written);
            bounds.Add(iter);
            while (iter != loc_tree.end()) {
                vn = iter.Current;
                if (vn.getSpace() != spc || vn.getOffset() > maxOff)
                    break;
                if (vn.isFree()) {
                    iter = endLoc(vn.getSize(), vn.getAddr(), 0);
                    continue;
                }
                ulong endOff = vn.getOffset() + (uint)(vn.getSize() - 1);
                if (endOff > maxOff)
                    maxOff = endOff;
                flags |= vn.getFlags();
                bounds.Add(iter);
                iter = endLoc(vn.getSize(), vn.getAddr(), Varnode.varnode_flags.written);
                bounds.Add(iter);
            }
            bounds.Add(iter);
            return flags;
        }

        /// Beginning of Varnodes sorted by definition
        public IEnumerator<Varnode> beginDef() => def_tree.GetEnumerator();

        //// End of Varnodes sorted by definition
        //public IEnumerator<Varnode> endDef() => def_tree.end();

        /// \brief Beginning of varnodes with set definition property
        ///
        /// Get an iterator to Varnodes in definition order restricted with the
        /// following properties:
        ///    - Varnode.varnode_flags.input for Varnodes which are inputs to the function
        ///    - Varnode.varnode_flags.written for Varnodes that are defined by a PcodeOp
        ///    - 0 for \e free Varnodes
        /// \param fl is the property restriction
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginDef(Varnode.varnode_flags fl)
        {
            IEnumerator<Varnode> iter;
            PcodeOp searchop;
            SeqNum sq;

            if (fl == Varnode.varnode_flags.input)
                // Inputs occur first with def_tree
                return def_tree.GetEnumerator();
            if (fl == Varnode.varnode_flags.written) {
                // Lowest possible location
                searchvn.loc = new Address(Address.mach_extreme.m_minimal);
                searchvn.flags = Varnode.varnode_flags.written;
                // Lowest possible seqnum
                sq = new SeqNum(Address.mach_extreme.m_minimal);
                searchop = new PcodeOp(0,sq);
                searchvn.def = searchop;
                iter = def_tree.lower_bound(searchvn);
                searchvn.flags = Varnode.varnode_flags.input; // Reset flags
                return iter;
            }

            // Find the start of the frees
            // Maximal possible location
            searchvn.loc = new Address(Address.mach_extreme.m_maximal);
            searchvn.flags = Varnode.varnode_flags.written;
            // Maximal seqnum
            sq = new SeqNum(Address.mach_extreme.m_maximal);
            searchop = new PcodeOp(0,sq);
            searchvn.def = searchop;
            iter = def_tree.upper_bound(searchvn);
            searchvn.flags = Varnode.varnode_flags.input; // Reset flags
            return iter;
        }

        /// \brief End of varnodes with set definition property
        ///
        /// Get an iterator to Varnodes in definition order restricted with the
        /// following properties:
        ///    - Varnode.varnode_flags.input for Varnodes which are inputs to the function
        ///    - Varnode.varnode_flags.written for Varnodes that are defined by a PcodeOp
        ///    - 0 for \e free Varnodes
        /// \param fl is the property restriction
        /// \return the ending iterator
        public IEnumerator<Varnode> endDef(Varnode.varnode_flags fl)
        {
            IEnumerator<Varnode> iter;

            if (fl == Varnode.varnode_flags.input) {
                // Highest input is lowest written
                searchvn.loc = new Address(Address.mach_extreme.m_minimal); // Lowest possible location
                searchvn.flags = Varnode.varnode_flags.written;
                SeqNum sq = new SeqNum(Address.mach_extreme.m_minimal); // Lowest possible seqnum
                PcodeOp searchop = new PcodeOp(0,sq);
                searchvn.def = searchop;
                iter = def_tree.lower_bound(searchvn);
                searchvn.flags = Varnode.varnode_flags.input; // Reset flags
                return iter;
            }
            else if (fl == Varnode.varnode_flags.written) {
                // Highest written
                searchvn.loc = new Address(Address.mach_extreme.m_maximal); // Maximal possible location
                searchvn.flags = Varnode.varnode_flags.written;
                SeqNum sq = new SeqNum(Address.mach_extreme.m_maximal); // Maximal seqnum
                PcodeOp searchop = new PcodeOp(0,sq);
                searchvn.def = searchop;
                iter = def_tree.upper_bound(searchvn);
                searchvn.flags = Varnode.varnode_flags.input; // Reset flags
                return iter;
            }
            return def_tree.end();  // Highest free is end of def_tree
        }

        /// \brief Beginning of varnodes starting at a given address with a set definition property
        ///
        /// Get an iterator to Varnodes in definition order.  The starting address of the Varnodes
        /// must match the given address, and they are further restricted by the
        /// following properties:
        ///    - Varnode.varnode_flags.input for Varnodes which are inputs to the function
        ///    - Varnode.varnode_flags.written for Varnodes that are defined by a PcodeOp
        ///    - 0 for \e free Varnodes
        /// \param fl is the property restriction
        /// \param addr is the given starting address
        /// \return the beginning iterator
        public IEnumerator<Varnode> beginDef(Varnode.varnode_flags fl, Address addr)
        {
            // Get varnodes with addr and with definition type
            IEnumerator<Varnode> iter;

            if (fl == Varnode.varnode_flags.written)
                throw new LowlevelError("Cannot get contiguous written AND addressed");
            else if (fl == Varnode.varnode_flags.input) {
                searchvn.loc = addr;
                iter = def_tree.lower_bound(searchvn);
                return iter;
            }

            // Find the start of the frees with a given address
            searchvn.loc = addr;
            searchvn.flags = 0;
            // Since a size 0 object shouldn't exist, an upper bound
            // should bump up to first free of addr with non-zero size
            iter = def_tree.upper_bound(searchvn);
            searchvn.flags = Varnode.varnode_flags.input; // Reset flags
            return iter;
        }

        /// \brief End of varnodes starting at a given address with a set definition property
        ///
        /// Get an iterator to Varnodes in definition order.  The starting address of the Varnodes
        /// must match the given address, and they are further restricted by the
        /// following properties:
        ///    - Varnode.varnode_flags.input for Varnodes which are inputs to the function
        ///    - Varnode.varnode_flags.written for Varnodes that are defined by a PcodeOp
        ///    - 0 for \e free Varnodes
        /// \param fl is the property restriction
        /// \param addr is the given starting address
        /// \return the ending iterator
        public IEnumerator<Varnode> endDef(Varnode.varnode_flags fl,Address addr)
        {
            IEnumerator<Varnode> iter;

            if (fl == Varnode.varnode_flags.written)
                throw new LowlevelError("Cannot get contiguous written AND addressed");
            else if (fl == Varnode.varnode_flags.input) {
                searchvn.loc = addr;
                searchvn.size = 1000000;
                iter = def_tree.lower_bound(searchvn);
                searchvn.size = 0;
                return iter;
            }

            // Find the start of the frees with a given address
            searchvn.loc = addr;
            searchvn.size = 1000000;
            searchvn.flags = 0;
            // Since a size 0 object shouldn't exist, an upper bound
            // should bump up to first free of addr with non-zero size
            iter = def_tree.lower_bound(searchvn);
            searchvn.flags = Varnode.varnode_flags.input; // Reset flags
            searchvn.size = 0;
            return iter;
        }

#if VARBANK_DEBUG
        /// Check tree order is still accurate
        èè/// Verify the integrity of the container
        public void verifyIntegrity()
        {
          VarnodeLocSet::iterator iter;
          Varnode *vn,*lastvn;

          if (loc_tree.empty()) return;
          iter = loc_tree.begin();
          lastvn = *iter++;
          if (def_tree.end() == def_tree.find(lastvn))
            throw new LowlevelError("Varbank first loc missing in def");
          for(;iter!=loc_tree.end();++iter) {
            vn = *iter;
            if (def_tree.end() == def_tree.find(vn))
              throw new LowlevelError("Varbank loc missing in def");
            if (*vn < *lastvn)
              throw new LowlevelError("Varbank locdef integrity test failed");
            lastvn = vn;
          }

          VarnodeDefSet::iterator diter;
          VarnodeCompareDefLoc cmp;

          diter = def_tree.begin();
          lastvn = *diter++;
          if (loc_tree.end() == loc_tree.find(lastvn))
            throw new LowlevelError("Varbank first def missing in loc");
          for(;diter!=def_tree.end();++diter) {
            vn = *diter;
            if (loc_tree.end() == loc_tree.find(vn))
              throw new LowlevelError("Varbank def missing in loc");
            if (cmp(vn,lastvn))
              throw new LowlevelError("Varbank defloc integrity test failed");
            lastvn = vn;
          }
        }
#endif
    }
}
