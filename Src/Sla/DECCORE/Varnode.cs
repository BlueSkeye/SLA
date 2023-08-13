using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A low-level variable or contiguous set of bytes described by an Address and a size
    ///
    /// A Varnode is the fundemental \e variable in the p-code language model.  A Varnode
    /// represents anything that holds data, including registers, stack locations,
    /// global RAM locations, and constants.  It is described most simply as a storage
    /// location for some number of bytes, and is identified by
    ///    - an Address  (an AddrSpace and an offset into that space) and
    ///    - a size in bytes
    ///
    /// In its raw form, the Varnode is referred to as \b free, and this pair uniquely identifies
    /// the Varnode, as determined by its comparison operators.  In terms of the
    /// Static Single Assignment (SSA) form for the decompiler analysis, the Varnode class also
    /// represents a node in the tree. In this case the Varnode is not free, and
    /// each individual write to a storage location, as per SSA form, creates a unique Varnode, which
    /// is represented by a separate instance, so there may be multiple Varnode instances
    /// with the same Address and size. 
    internal class Varnode
    {
        /// There are a large number of boolean attributes that can be placed on a Varnode.
        /// Some are calculated and maintained by the friend classes Funcdata and VarnodeBank, 
        /// and others can be set and cleared publicly by separate subsystems.
        [Flags()]
        public enum varnode_flags : uint
        {
            /// Prevents infinite loops
            mark = 0x01,
            /// The varnode is constant
            constant = 0x02,
            /// This varnode is an annotation and has no dataflow
            annotation = 0x04,
            /// This varnode has no ancestor
            input = 0x08,
            /// This varnode has a defining op (def is nonzero)
            written = 0x10,
            /// This varnode has been inserted in a tree
            /// This means the varnode is the output of an op \e or
            /// The output is a constant \e or the output is an input
            insert = 0x20,
            /// This varnode is a temporary variable
            implied = 0x40,
            /// This varnode \e CANNOT be a temporary variable
            explict = 0x80,
            /// The Dataype of the Varnode is locked
            typelock = 0x100,
            /// The Name of the Varnode is locked
            namelock = 0x200,
            /// There are no aliases pointing to this varnode
            nolocalalias = 0x400,
            /// This varnode's value is volatile
            volatil = 0x800,
            /// Varnode address is specially mapped by the loader
            externref = 0x1000,
            /// Varnode is stored at a readonly location
            @readonly = 0x2000,
            /// Persists after (and before) function
            persist = 0x4000,
            /// High-level variable is tied to address
            addrtied = 0x8000,
            /// Input which is unaffected by the function
            unaffected = 0x10000,
            /// This is a base register for an address space
            spacebase = 0x20000,
            /// If all uses of illegalinput varnode are inputs to INDIRECT
            indirectonly = 0x40000,
            /// (could be) Directly affected by a valid input
            directwrite = 0x80000,
            /// Varnode is used to force variable into an address
            addrforce = 0x100000,
            /// Varnode has a database entry associated with it
            mapped = 0x200000,
            /// The value in this Varnode is created indirectly
            indirect_creation = 0x400000,
            /// Is the varnode storage for a return address
            return_address = 0x800000,
            /// Cover is not upto date
            coverdirty = 0x1000000,
            /// Is this Varnode the low part of a double precision value
            precislo = 0x2000000,
            /// Is this Varnode the high part of a double precision value
            precishi = 0x4000000,
            /// Is this Varnode storing a pointer to the actual symbol
            indirectstorage = 0x8000000,
            /// Does this varnode point to the return value storage location
            hiddenretparm = 0x10000000,
            /// Do copies of this varnode happen as a side-effect
            incidental_copy = 0x20000000,
            /// Temporarily block dead-code removal of \b this
            autolive_hold = 0x40000000,
            /// Varnode is getting PIECEd together into an (unmapped) structure
            proto_partial = 0x80000000
        }

        /// Additional boolean properties on a Varnode
        [Flags()]
        public enum addl_flags
        {
            /// The varnode is actively being heritaged
            activeheritage = 0x01,
            /// Should not be considered a write in heritage calculation
            writemask = 0x02,
            /// Vacuous consume
            vacconsume = 0x04,
            /// In consume worklist
            lisconsume = 0x08,
            /// The Varnode value is \e NOT a pointer
            ptrcheck = 0x10,
            /// If this varnode flows to or from a pointer
            ptrflow = 0x20,
            /// Constant that must be explicitly printed as an unsigned token
            unsignedprint = 0x40,
            /// Constant that must be explicitly printed as a \e long integer token
            longprint = 0x80,
            /// Created by an explicit STORE
            stack_store = 0x100,
            /// Input that exists even if its unused
            locked_input = 0x200,
            /// This varnode is inserted artificially to track a register
            /// value at a specific point in the code
            spacebase_placeholder = 0x400,
            /// Data-types do not propagate from an output into \b this
            stop_uppropagation = 0x800,
            /// The varnode is implied but also has a data-type that needs resolution
            has_implied_field = 0x1000
        }

        /// The collection of boolean attributes for this Varnode
        internal /*mutable*/ varnode_flags flags;
        /// Size of the Varnode in bytes
        internal int size;
        /// A unique one-up index assigned to Varnode at its creation
        private uint create_index;
        /// Which group of forced merges does this Varnode belong to
        private short mergegroup;
        /// Additional flags
        private Varnode.addl_flags addlflags;
        /// Storage location (or constant value) of the Varnode
        internal Address loc;

        // Heritage fields
        /// The defining operation of this Varnode
        internal PcodeOp? def;
        /// High-level variable of which this is an instantiation
        private HighVariable? high;
        /// cached SymbolEntry associated with Varnode
        private SymbolEntry mapentry;
        /// Datatype associated with this varnode
        private Datatype type;
        /// Iterator into VarnodeBank sorted by location
        private VarnodeLocSet::iterator lociter;
        /// Iterator into VarnodeBank sorted by definition
        private VarnodeDefSet::iterator defiter;
        /// List of every op using this varnode as input
        internal List<PcodeOp> descend;
        /// Addresses covered by the def.use of this Varnode
        internal /*mutable*/ Cover? cover;
        
        private /*mutable*/ struct TempStorage
        {
            /// Temporary data-type associated with \b this for use in type propagate algorithm
            internal Datatype dataType;
            /// Value set associated with \b this when performing Value Set Analysis
            internal ValueSet valueSet;
        }
        /// Temporary storage for analysis algorithms
        private TempStorage temp;

        /// What parts of this varnode are used
        private ulong consumed;
        /// Which bits do we know are zero
        internal ulong nzm;
        //friend class VarnodeBank;
        //friend class Merge;
        //friend class Funcdata;

        /// Internal function for update coverage information
        /// Rebuild variable cover based on where the Varnode
        /// is defined and read. This is \e only called by the
        /// Merge class which knows when to call it properly
        private void updateCover()
        {
            if ((flags & varnode_flags.coverdirty) != 0) {
                if (hasCover() && (cover != (Cover)null))
                    cover.rebuild(this);
                clearFlags(varnode_flags.coverdirty);
            }
        }

        /// Turn on the Cover object for this Varnode
        /// Initialize a new Cover and set dirty bit so that updateCover will rebuild
        internal void calcCover()
        {
            if (hasCover()) {
                if (cover != (Cover)null)
                    // delete cover;
                cover = new Cover();
                setFlags(Varnode.varnode_flags.coverdirty);
            }
        }

        /// Turn off any coverage information
        /// Delete the Cover object.  Used for dead Varnodes before full deletion.
        internal void clearCover()
        {
            if (cover != (Cover)null) {
                // delete cover;
                cover = (Cover)null;
            }
        }

        /// Internal method for setting boolean attributes
        /// Set desired boolean attributes on this Varnode and then set dirty bits if appropriate
        /// \param fl is the mask containing the list of attributes to set
        internal void setFlags(varnode_flags fl)
        {
            flags |= fl;
            if (high != (HighVariable)null) {
                high.flagsDirty();
                if ((fl & varnode_flags.coverdirty) != 0)
                    high.coverDirty();
            }
        }

        /// Internal method for clearing boolean attributes
        /// Clear desired boolean attributes on this Varnode and then set dirty bits if appropriate
        /// \param fl is the mask containing the list of attributes to clear
        internal void clearFlags(varnode_flags fl)
        {
            flags &= ~fl;
            if (high != (HighVariable)null) {
                high.flagsDirty();
                if ((fl & Varnode.varnode_flags.coverdirty) != 0)
                    high.coverDirty();
            }
        }

        /// Clear any Symbol attached to \b this Varnode
        /// For \b this Varnode and any others attached to the same HighVariable,
        /// remove any SymbolEntry reference and associated properties.
        internal void clearSymbolLinks()
        {
            bool foundEntry = false;
            for (int i = 0; i < high.numInstances(); ++i) {
                Varnode vn = high.getInstance(i);
                foundEntry = foundEntry || (vn.mapentry != (SymbolEntry)null);
                vn.mapentry = (SymbolEntry)null;
                vn.clearFlags(Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock | Varnode.varnode_flags.mapped);
            }
            if (foundEntry)
                high.symbolDirty();
        }

        /// Mark Varnode as \e unaffected
        internal void setUnaffected()
        {
            setFlags(Varnode.varnode_flags.unaffected);
        }

        // These functions should be only private things used by VarnodeBank
        /// Mark Varnode as \e input
        private void setInput()
        {
            setFlags(Varnode.varnode_flags.input | Varnode.varnode_flags.coverdirty);
        }

        /// Set the defining PcodeOp of this Varnode
        /// Directly change the defining PcodeOp and set appropriate dirty bits
        /// \param op is the pointer to the new PcodeOp, which can be \b null
        private void setDef(PcodeOp op)
        {               // Set the defining op
            def = op;
            if (op == (PcodeOp)null) {
                setFlags(Varnode.varnode_flags.coverdirty);
                clearFlags(Varnode.varnode_flags.written);
            }
            else
                setFlags(Varnode.varnode_flags.coverdirty | Varnode.varnode_flags.written);
        }

        /// Set properties from the given Symbol to \b this Varnode
        /// The given Symbol's data-type and flags are inherited by \b this Varnode.
        /// If the Symbol is \e type-locked, a reference to the Symbol is set on \b this Varnode.
        /// \param entry is a mapping to the given Symbol
        /// \return \b true if any properties have changed
        internal bool setSymbolProperties(SymbolEntry entry)
        {
            bool res = entry.updateType(this);
            if (entry.getSymbol().isTypeLocked())
            {
                if (mapentry != entry)
                {
                    mapentry = entry;
                    if (high != (HighVariable)null)
                        high.setSymbol(this);
                    res = true;
                }
            }
            setFlags(entry.getAllFlags() & ~Varnode.varnode_flags.typelock);
            return res;
        }

        /// Attach a Symbol to \b this Varnode
        /// A reference to the given Symbol is set on \b this Varnode.
        /// The data-type on \b this Varnode is not changed.
        /// \param entry is a mapping to the given Symbol
        internal void setSymbolEntry(SymbolEntry entry)
        {
            mapentry = entry;
            varnode_flags fl = varnode_flags.mapped; // Flags are generally not changed, but we do mark this as mapped
            if (entry.getSymbol().isNameLocked())
                fl |= varnode_flags.namelock;
            setFlags(fl);
            if (high != (HighVariable)null)
                high.setSymbol(this);
        }

        /// Attach a Symbol reference to \b this
        /// Link Symbol information to \b this as a \b reference. This only works for a constant Varnode.
        /// This used when there is a constant address reference to the Symbol and the Varnode holds the
        /// reference, not the actual value of the Symbol.
        /// \param entry is a mapping to the given Symbol
        /// \param off is the byte offset into the Symbol of the reference
        internal void setSymbolReference(SymbolEntry entry, int off)
        {
            if (high != (HighVariable)null) {
                high.setSymbolReference(entry.getSymbol(), off);
            }
        }

        /// Add a descendant (reading) PcodeOp to this Varnode's list
        /// Put a new operator in the descendant list and set the cover dirty flag
        /// \param op is PcodeOp to add
        internal void addDescend(PcodeOp op)
        {
            //  if (!heritageknown()) {
            if (isFree() && (!isSpacebase()))
            {
                if (!descend.empty())
                    throw new LowlevelError("Free varnode has multiple descendants");
            }
            descend.Add(op);
            setFlags(varnode_flags.coverdirty);
        }

        /// Erase a descendant (reading) PcodeOp from this Varnode's list
        /// Erase the operation from our descendant list and set the cover dirty flag
        /// \param op is the PcodeOp to remove
        internal void eraseDescend(PcodeOp op)
        {
            descend.Remove(op);        // Remove it from list
            setFlags(varnode_flags.coverdirty);
        }

        /// Clear all descendant (reading) PcodeOps
        /// Completely clear the descendant list
        /// Only called if Varnode is about to be irrevocably destroyed
        internal void destroyDescend()
        {
            descend.Clear();
        }

        // only to be used by HighVariable
        /// Set the HighVariable owning this Varnode
        public void setHigh(HighVariable tv, short mg)
        {
            high = tv;
            mergegroup = mg;
        }

        /// Get the storage Address
        public Address getAddr() => (Address)loc;

        /// Get the AddrSpace storing this Varnode
        public AddrSpace getSpace() => loc.getSpace();

        /// Get AddrSpace from \b this encoded constant Varnode
        /// In \b LOAD and \b STORE instructions, the particular address space being read/written is encoded
        /// as a constant Varnode.  Internally, this constant is the actual pointer to the AddrSpace.
        /// \return the AddrSpace pointer
        public AddrSpace getSpaceFromConst() => (AddrSpace)(ulong)loc.getOffset();

        /// Get the offset (within its AddrSpace) where this is stored
        public ulong getOffset() => loc.getOffset();

        /// Get the number of bytes this Varnode stores
        public int getSize() => size;

        /// Get the \e forced \e merge group of this Varnode
        public short getMergeGroup() => mergegroup;

        /// Get the defining PcodeOp of this Varnode
        public PcodeOp? getDef() => def;

        /// Get the high-level variable associated with this Varnode
        /// During the course of analysis Varnodes are merged into high-level variables that are intended
        /// to be closer to the concept of variables in C source code. For a large portion of the decompiler
        /// analysis this concept hasn't been built yet, and this routine will return \b null.
        /// But after a certain point, every Varnode managed by the Funcdata object, with the exception
        /// of ones that are marked as \e annotations, is associated with some HighVariable
        /// and will return a non-null result.
        /// \return the associated HighVariable
        public HighVariable getHigh()
        {
            if (high == (HighVariable)null)
                throw new LowlevelError("Requesting non-existent high-level");
            return high;
        }

        /// Get symbol and scope information associated with this Varnode
        public SymbolEntry getSymbolEntry() => mapentry;

        /// Get all the boolean attributes
        public varnode_flags getFlags() => flags;

        /// Get the Datatype associated with this Varnode
        public Datatype getType() => type;

        /// Return the data-type of \b this when it is written to
        /// This generally just returns the data-type of the Varnode itself unless it is a \e union data-type.
        /// In this case, the data-type of the resolved field of the \e union, associated with writing to the Varnode,
        /// is returned. The Varnode \b must be written to, to call this method.
        /// \return the resolved data-type
        public Datatype getTypeDefFacing()
        {
            if (!type.needsResolution())
                return type;
            return type.findResolve(def, -1);
        }

        /// Get the data-type of \b this when it is read by the given PcodeOp
        /// This generally just returns the data-type of the Varnode itself unless it is a \e union data-type.
        /// In this case, the data-type of the resolved field of the \e union, associated with reading the Varnode,
        /// is returned.
        /// \param op is the PcodeOp reading \b this Varnode
        /// \return the resolved data-type
        public Datatype getTypeReadFacing(PcodeOp op)
        {
            if (!type.needsResolution())
                return type;
            return type.findResolve(op, op.getSlot(this));
        }

        /// Return the data-type of the HighVariable when \b this is written to
        /// This generally just returns the data-type of the HighVariable associated with \b this, unless it is a
        /// \e union data-type. In this case, the data-type of the resolved field of the \e union, associated with
        /// writing to the Varnode, is returned.
        /// \return the resolved data-type
        public Datatype getHighTypeDefFacing()
        {
            Datatype ct = high.getType() ?? throw new BugException();
            if (!ct.needsResolution())
                return ct;
            return ct.findResolve(def, -1);
        }

        /// Return data-type of the HighVariable when read by the given PcodeOp
        /// This generally just returns the data-type of the HighVariable associated with \b this, unless it is a
        /// \e union data-type. In this case, the data-type of the resolved field of the \e union, associated with
        /// reading the Varnode, is returned.
        /// \param op is the PcodeOp reading \b this Varnode
        /// \return the resolved data-type
        public Datatype getHighTypeReadFacing(PcodeOp op)
        {
            Datatype ct = high.getType() ?? throw new BugException();
            if (!ct.needsResolution())
                return ct;
            return ct.findResolve(op, op.getSlot(this));
        }

        /// Set the temporary Datatype
        public void setTempType(Datatype t)
        {
            temp.dataType = t;
        }

        /// Get the temporary Datatype (used during type propagation)
        public Datatype getTempType() => temp.dataType;

        /// Set the temporary ValueSet record
        public void setValueSet(ValueSet v)
        {
            temp.valueSet = v;
        }

        /// Get the temporary ValueSet record
        public ValueSet getValueSet() => temp.valueSet;

        /// Get the creation index
        public uint getCreateIndex() => create_index;

        /// Get Varnode coverage information
        public Cover? getCover()
        {
            updateCover();
            return cover;
        }

        /// Get iterator to list of syntax tree descendants (reads)
        public IEnumerator<PcodeOp> beginDescend() => descend.GetEnumerator();

        /// Get the end iterator to list of descendants
        public IEnumerator<PcodeOp> endDescend() => descend.end();

        /// Get mask of consumed bits
        public ulong getConsume() => consumed;

        /// Set the mask of consumed bits (used by dead-code algorithm)
        public void setConsume(ulong val)
        {
            consumed = val;
        }

        /// Get marker used by dead-code algorithm
        public bool isConsumeList() => ((addlflags & Varnode.addl_flags.lisconsume) != 0);

        /// Get marker used by dead-code algorithm
        public bool isConsumeVacuous() => ((addlflags & Varnode.addl_flags.vacconsume) != 0);

        /// Set marker used by dead-code algorithm
        public void setConsumeList()
        {
            addlflags |= Varnode.addl_flags.lisconsume;
        }

        /// Set marker used by dead-code algorithm
        public void setConsumeVacuous()
        {
            addlflags |= Varnode.addl_flags.vacconsume;
        }

        /// Clear marker used by dead-code algorithm
        public void clearConsumeList()
        {
            addlflags &= ~Varnode.addl_flags.lisconsume;
        }

        /// Clear marker used by dead-code algorithm
        public void clearConsumeVacuous()
        {
            addlflags &= ~Varnode.addl_flags.vacconsume;
        }

        /// Return unique reading PcodeOp, or \b null if there are zero or more than 1
        /// This is a convenience method for quickly finding the unique PcodeOp that reads this Varnode
        /// \return only descendant (if there is 1 and ONLY 1) or \b null otherwise
        public PcodeOp? loneDescend()
        {
            if (descend.empty()) return (PcodeOp)null; // No descendants
            return (1 == descend.Count) ? descend[0] : (PcodeOp)null; // More than 1 descendant
        }

        /// Get Address when this Varnode first comes into scope
        /// A Varnode can be defined as "coming into scope" at the Address of the first PcodeOp that
        /// writes to that storage location.  Within SSA form this \b first-use address always exists and
        /// is unique if we consider inputs to come into scope at the start Address of the function they are in
        /// \param fd is the Funcdata containing the tree
        /// \return the first-use Address
        public Address getUsePoint(Funcdata fd)
        {
            if (isWritten())
                return def.getAddr();
            return fd.getAddress() + -1;
            //  return loc.getSpace().getTrans().constant(0);
        }

        /// Print a simple identifier for the Varnode
        /// Print to the stream either the name of the Varnode, such as a register name, if it exists
        /// or print a shortcut character representing the AddrSpace and a hex representation of the offset.
        /// This function also computes and returns the \e expected size of the identifier it prints
        /// to facilitate the printing of size modifiers by other print routines
        /// \param s is the output stream
        /// \return the expected size
        public int printRawNoMarkup(TextWriter s)
        {
            AddrSpace spc = loc.getSpace();
            Translate trans = spc.getTrans();
            string name;
            int expect;

            name = trans.getRegisterName(spc, loc.getOffset(), size);
            if (name.Length != 0) {
                VarnodeData point = trans.getRegister(name);
                ulong off = loc.getOffset() - point.offset;
                s.Write(name);
                expect = (int)point.size;
                if (off != 0)
                    s.Write($"+{off}");
            }
            else {
                s.Write(loc.getShortcut()); // Print type shortcut character
                expect = (int)trans.getDefaultSize();
                loc.printRaw(s);
            }
            return expect;
        }

        /// Print a simple identifier plus additional info identifying Varnode with SSA form
        /// Print textual information about this Varnode including a base identifier along with enough
        /// size and attribute information to uniquely identify the Varnode within a text SSA listing
        /// In particular, the identifiers have either "i" or defining op SeqNum information appended
        /// to them in parantheses.
        /// \param s is the output stream
        public void printRaw(TextWriter s)
        {
            int expect = printRawNoMarkup(s);

            if (expect != size)
                s.Write($":{size}");
            if ((flags & Varnode.varnode_flags.input) != 0)
                s.Write("(i)");
            if (isWritten())
                s.Write($"({def.getSeqNum()})");
            if ((flags & (Varnode.varnode_flags.insert | Varnode.varnode_flags.constant)) == 0) {
                s.Write("(free)");
                return;
            }
        }

        /// Print raw coverage info about the Varnode
        /// Print, to a stream, textual information about where \b this Varnode is in scope within its
        /// particular Funcdata. This amounts to a list of address ranges bounding the writes and reads
        /// of the Varnode
        /// \param s is the output stream
        public void printCover(TextWriter s)
        {
            if (cover == (Cover)null)
                throw new LowlevelError("No cover to print");
            if ((flags & Varnode.varnode_flags.coverdirty) != 0)
                s.WriteLine("Cover is dirty");
            else
                cover.print(s);
        }

        /// Print raw attribute info about the Varnode
        /// Print boolean attribute information about \b this as keywords to a stream
        /// \param s is the output stream
        public void printInfo(TextWriter s)
        {
            type.printRaw(s);
            s.Write(" = ");
            printRaw(s);
            if (isAddrTied())
                s.Write(" tied");
            if (isMapped())
                s.Write(" mapped");
            if (isPersist())
                s.Write(" persistent");
            if (isTypeLock())
                s.Write(" tlock");
            if (isNameLock())
                s.Write(" nlock");
            if (isSpacebase())
                s.Write(" base");
            if (isUnaffected())
                s.Write(" unaff");
            if (isImplied())
                s.Write(" implied");
            if (isAddrForce())
                s.Write(" addrforce");
            if (isReadOnly())
                s.Write(" readonly");
            s.Write($" (consumed=0x{consumed:X})");
            // s.Write(" (internal={this:X})");
            s.Write(" (create=0x{create_index:X})");
            s.WriteLine();
        }

        /// Construct a \e free Varnode
        /// This is the constructor for making an unmanaged Varnode
        /// It creates a \b free Varnode with possibly a Datatype attribute.
        /// Most applications create Varnodes through the Funcdata interface
        /// \param s is the size of the new Varnode
        /// \param m is the starting storage Address
        /// \param dt is the Datatype
        public Varnode(int s, Address m, Datatype dt)
        {
            // Construct a varnode
            loc = m;
            size = s;
            def = (PcodeOp)null;      // No defining op yet
            type = dt;
            high = (HighVariable)null;
            mapentry = (SymbolEntry)null;
            consumed = ulong.MaxValue;
            cover = (Cover)null;
            mergegroup = 0;
            addlflags = 0;
            if (m.getSpace() == (AddrSpace)null) {
                flags = 0;
                return;
            }
            spacetype tp = m.getSpace().getType();
            if (tp == spacetype.IPTR_CONSTANT) {
                flags = Varnode.varnode_flags.constant;
                nzm = m.getOffset();
            }
            else if ((tp == spacetype.IPTR_FSPEC) || (tp == spacetype.IPTR_IOP)) {
                flags = Varnode.varnode_flags.annotation | Varnode.varnode_flags.coverdirty;
                nzm = ulong.MaxValue;
            }
            else {
                flags = Varnode.varnode_flags.coverdirty;
                nzm = ulong.MaxValue;
            }
        }

        /// Comparison operator on Varnode
        /// Compare two Varnodes
        ///    - First by storage location
        ///    - Second by size
        ///    - Then by defining PcodeOp SeqNum if appropriate
        ///
        /// \e Input Varnodes come before \e written Varnodes
        /// \e Free Varnodes come after everything else
        /// \param op2 is the Varnode to compare \b this to
        /// \return \b true if \b this is less than \b op2
        public static bool operator <(Varnode op1, Varnode op2)
        {
            if (op1.loc != op2.loc) return (op1.loc < op2.loc);
            if (op1.size != op2.size) return (op1.size < op2.size);
            varnode_flags f1 = op1.flags & (varnode_flags.input | varnode_flags.written);
            varnode_flags f2 = op2.flags & (varnode_flags.input | varnode_flags.written);
            if (f1 != f2) return ((f1 - 1) < (f2 - 1)); // -1 forces free varnodes to come last
            if (f1 == Varnode.varnode_flags.written)
                if (op1.def.getSeqNum() != op2.def.getSeqNum())
                    return (op1.def.getSeqNum() < op2.def.getSeqNum());
            return false;
        }

        /// Equality operator
        /// Determine if two Varnodes are equivalent.  They must match
        ///    - Storage location
        ///    - Size
        ///    - Defining PcodeOp if it exists
        ///
        /// \param op2 is the Varnode to compare \b this to
        /// \return true if they are equivalent
        public static bool operator ==(Varnode op1, Varnode op2)
        {
            // Compare two varnodes
            if (op1.loc != op2.loc) return false;
            if (op1.size != op2.size) return false;
            varnode_flags f1 = op1.flags & (varnode_flags.input | varnode_flags.written);
            varnode_flags f2 = op2.flags & (varnode_flags.input | varnode_flags.written);
            if (f1 != f2) return false;
            if (f1 == varnode_flags.written)
                if (op1.def.getSeqNum() != op2.def.getSeqNum()) return false;
            return true;
        }

        /// Inequality operator
        public static bool operator !=(Varnode op1, Varnode op2) => !(op1 == op2);

        /// Delete the Varnode object. This routine assumes all other cross-references have been removed.
        ~Varnode()
        {
            //if (cover != (Cover)null)
            //    delete cover;
            if (high != (HighVariable)null) {
                high.remove(this);
                //if (high.isUnattached())
                //    delete high;
            }
        }

        /// Return \b true if the storage locations intersect
        /// Check whether the storage locations of two varnodes intersect
        /// \param op is the Varnode to compare with \b this
        /// \return \b true if the locations intersect
        public bool intersects(Varnode op)
        {
            if (loc.getSpace() != op.loc.getSpace()) return false;
            if (loc.getSpace().getType() == spacetype.IPTR_CONSTANT) return false;
            ulong a = loc.getOffset();
            ulong b = op.loc.getOffset();
            if (b < a) {
                if (a >= (ulong)((int)b + op.size)) return false;
                return true;
            }
            if (b >= (ulong)((int)a + size)) return false;
            return true;
        }

        /// Check intersection against an Address range
        /// Check if \b this intersects the given Address range
        /// \param op2loc is the start of the range
        /// \param op2size is the size of the range in bytes
        /// \return \b true if \b this intersects the range
        public bool intersects(Address op2loc, int op2size)
        {
            if (loc.getSpace() != op2loc.getSpace()) return false;
            if (loc.getSpace().getType() == spacetype.IPTR_CONSTANT) return false;
            ulong a = loc.getOffset();
            ulong b = op2loc.getOffset();
            if (b < a) {
                if (a >= b + (uint)op2size) return false;
                return true;
            }
            if (b >= a + (uint)size) return false;
            return true;
        }

        /// Return info about the containment of \e op in \b this
        /// Return various values depending on the containment of another Varnode within \b this.
        /// Return
        ///         -  -1 if op.loc starts before -this-
        ///         -   0 if op is contained in -this-
        ///         -   1 if op.start is contained in -this-
        ///         -   2 if op.loc comes after -this- or
        ///         -   3 if op and -this- are in non-comparable spaces
        /// \param op is the Varnode to test for containment
        /// \return the integer containment code
        public int contains(Varnode op)
        {
            if (loc.getSpace() != op.loc.getSpace()) return 3;
            if (loc.getSpace().getType() == spacetype.IPTR_CONSTANT) return 3;
            ulong a = loc.getOffset();
            ulong b = op.loc.getOffset();
            if (b < a) return -1;
            if (b >= a + (uint)size) return 2;
            if (b + (uint)op.size > a + (uint)size) return 1;
            return 0;
        }

        /// Return 0, 1, or 2 for "no overlap", "partial overlap", "identical storage"
        public int characterizeOverlap(Varnode op)
        {
            if (loc.getSpace() != op.loc.getSpace())
                return 0;
            if (loc.getOffset() == op.loc.getOffset())      // Left sides match
                return (size == op.size) ? 2 : 1;   // Either total match or partial
            else if (loc.getOffset() < op.loc.getOffset()) {
                ulong thisright = loc.getOffset() + (uint)(size - 1);
                return (thisright < op.loc.getOffset()) ? 0 : 1;        // Test if this ends before op begins
            }
            else {
                ulong opright = op.loc.getOffset() + (uint)(op.size - 1);
                return (opright < loc.getOffset()) ? 0 : 1;         // Test if op ends before this begins
            }
        }

        /// Return relative point of overlap between two Varnodes
        /// Return whether \e Least \e Signifigant \e Byte of \b this occurs in \b op
        /// I.e. return
        ///     - 0 if it overlaps op's lsb
        ///     - 1 if it overlaps op's second lsb  and so on
        /// \param op is the Varnode to test for overlap
        /// \return the relative overlap point or -1
        public int overlap(Varnode op)
        {
            if (!loc.isBigEndian()) // Little endian
                return loc.overlap(0, op.loc, op.size);
            else {
                // Big endian
                int over = loc.overlap(size - 1, op.loc, op.size);
                if (over != -1)
                    return op.size - 1 - over;
            }
            return -1;
        }

        /// Return relative point of overlap, where the given Varnode may be in the \e join space
        /// Return whether \e Least \e Signifigant \e Byte of \b this occurs in \b op.
        /// If \b op is in the \e join space, \b this can be in one of the pieces associated with the \e join range, and
        /// the offset returned will take into account the relative position of the piece within the whole \e join.
        /// Otherwise, this method is equivalent to Varnode::overlap.
        /// \param op is the Varnode to test for overlap
        /// \return the relative overlap point or -1
        public int overlapJoin(Varnode op)
        {
            if (!loc.isBigEndian()) // Little endian
                return loc.overlapJoin(0, op.loc, op.size);
            else {           // Big endian
                int over = loc.overlapJoin(size - 1, op.loc, op.size);
                if (over != -1)
                    return op.size - 1 - over;
            }
            return -1;
        }

        /// Return relative point of overlap with Address range
        /// Return whether \e Least \e Signifigant \e Byte of \b this occurs in an Address range
        /// I.e. return
        ///     - 0 if it overlaps op's lsb
        ///     - 1 if it overlaps op's second lsb  and so on
        /// \param op2loc is the starting Address of the range
        /// \param op2size is the size of the range in bytes
        /// \return the relative overlap point or -1
        public int overlap(Address op2loc, int op2size)
        {
            if (!loc.isBigEndian()) // Little endian
                return loc.overlap(0, op2loc, op2size);
            else {
                // Big endian
                int over = loc.overlap(size - 1, op2loc, op2size);
                if (over != -1)
                    return op2size - 1 - over;
            }
            return -1;
        }

        /// Get the mask of bits within \b this that are known to be zero
        public ulong getNZMask() => nzm;

        /// Compare two Varnodes based on their term order
        /// Compare term order of two Varnodes. Used in Term Rewriting strategies to order operands of commutative ops
        /// \param op is the Varnode to order against \b this
        /// \return -1 if \b this comes before \b op, 1 if op before this, or 0
        public int termOrder(Varnode op)
        {
            if (isConstant()) {
                if (!op.isConstant()) return 1;
            }
            else {
                if (op.isConstant()) return -1;
                Varnode vn = this;
                if (vn.isWritten() && (vn.getDef().code() == OpCode.CPUI_INT_MULT))
                    if (vn.getDef().getIn(1).isConstant())
                        vn = vn.getDef().getIn(0);
                if (op.isWritten() && (op.getDef().code() == OpCode.CPUI_INT_MULT))
                    if (op.getDef().getIn(1).isConstant())
                        op = op.getDef().getIn(0);

                if (vn.getAddr() < op.getAddr()) return -1;
                if (op.getAddr() < vn.getAddr()) return 1;
            }
            return 0;
        }

        /// Print a simple SSA subtree rooted at \b this
        /// Recursively print a terse textual representation of the data-flow (SSA) tree rooted at this Varnode
        /// \param s is the output stream
        /// \param depth is the current depth of the tree we are at
        public void printRawHeritage(TextWriter s, int depth)
        {
            for (int i = 0; i < depth; ++i)
                s.Write(' ');

            if (isConstant()) {
                printRaw(s);
                s.WriteLine();
                return;
            }
            printRaw(s);
            s.Write(' ');
            if (def != (PcodeOp)null)
                def.printRaw(s);
            else
                printRaw(s);

            if ((flags & Varnode.varnode_flags.input) != 0)
                s.Write(" Input");
            if ((flags & Varnode.varnode_flags.constant) != 0)
                s.Write(" Constant");
            if ((flags & Varnode.varnode_flags.annotation) != 0)
                s.Write(" Code");

            if (def != (PcodeOp)null) {
                s.WriteLine("\t\t{def.getSeqNum()}");
                for (int i = 0; i < def.numInput(); ++i)
                    def.getIn(i).printRawHeritage(s, depth + 5);
            }
            else
                s.WriteLine();
        }

        /// Is \b this an annotation?
        public bool isAnnotation() => ((flags & Varnode.varnode_flags.annotation) != 0);

        /// Is \b this an implied variable?
        public bool isImplied() => ((flags & Varnode.varnode_flags.implied) != 0);

        /// Is \b this an explicitly printed variable?
        public bool isExplicit() => ((flags & Varnode.varnode_flags.explict) != 0);

        /// Is \b this a constant?
        public bool isConstant() => ((flags & Varnode.varnode_flags.constant) != 0);

        /// Is \b this free, not in SSA form?
        public bool isFree() => ((flags & (Varnode.varnode_flags.written | Varnode.varnode_flags.input)) == 0);

        /// Is \b this an SSA input node?
        public bool isInput() => ((flags & Varnode.varnode_flags.input) != 0);

        /// Is \b this an abnormal input to the function?
        public bool isIllegalInput() => ((flags & (varnode_flags.input | varnode_flags.directwrite)) == Varnode.varnode_flags.input);

        /// Is \b this read only by INDIRECT operations?
        public bool isIndirectOnly() => ((flags & varnode_flags.indirectonly) != 0);

        /// Is \b this storage location mapped by the loader to an external location?
        public bool isExternalRef() => ((flags & varnode_flags.externref) != 0);

        /// Will this Varnode be replaced dynamically?
        public bool hasActionProperty() => ((flags & (varnode_flags.@readonly | varnode_flags.volatil)) != 0);

        /// Is \b this a read-only storage location?
        public bool isReadOnly() => ((flags & Varnode.varnode_flags.@readonly) != 0);

        /// Is \b this a volatile storage location?
        public bool isVolatile() => ((flags & Varnode.varnode_flags.volatil) != 0);

        /// Does \b this storage location persist beyond the end of the function?
        public bool isPersist() => ((flags & Varnode.varnode_flags.persist) != 0);

        /// Is \b this value affected by a legitimate function input
        public bool isDirectWrite() => ((flags & varnode_flags.directwrite) != 0);

        /// Are all Varnodes at this storage location components of the same high-level variable?
        public bool isAddrTied() => ((flags & (varnode_flags.addrtied | varnode_flags.insert)) == (varnode_flags.addrtied | varnode_flags.insert));

        /// Is \b this value forced into a particular storage location?
        public bool isAddrForce() => ((flags & varnode_flags.addrforce) != 0);

        /// Is \b this varnode exempt from dead-code removal?
        public bool isAutoLive() => ((flags & (varnode_flags.addrforce | varnode_flags.autolive_hold)) != 0);

        /// Is there a temporary hold on dead-code removal?
        public bool isAutoLiveHold() => ((flags & varnode_flags.autolive_hold) != 0);

        /// Is there or should be formal symbol information associated with \b this?
        public bool isMapped() => ((flags & varnode_flags.mapped) != 0);

        /// Is \b this a value that is supposed to be preserved across the function?
        public bool isUnaffected() => ((flags & varnode_flags.unaffected) != 0);

        /// Is this location used to store the base point for a virtual address space?
        public bool isSpacebase() => ((flags & varnode_flags.spacebase) != 0);

        /// Is this storage for a calls return address?
        public bool isReturnAddress() => ((flags & varnode_flags.return_address) != 0);

        /// Is \b this getting pieced together into a larger whole
        public bool isProtoPartial() => ((flags & varnode_flags.proto_partial) != 0);

        /// Has \b this been checked as a constant pointer to a mapped symbol?
        public bool isPtrCheck() => ((addlflags & addl_flags.ptrcheck) != 0);

        /// Does this varnode flow to or from a known pointer
        public bool isPtrFlow() => ((addlflags & addl_flags.ptrflow) != 0);

        /// Is \b this used specifically to track stackpointer values?
        public bool isSpacebasePlaceholder() => ((addlflags & addl_flags.spacebase_placeholder) != 0);

        /// Are there (not) any local pointers that might affect \b this?
        public bool hasNoLocalAlias() => ((flags & varnode_flags.nolocalalias) != 0);

        /// Has \b this been visited by the current algorithm?
        public bool isMark() => ((flags & varnode_flags.mark) != 0);

        /// Is \b this currently being traced by the Heritage algorithm?
        public bool isActiveHeritage() => ((addlflags & addl_flags.activeheritage) != 0);

        /// Was this originally produced by an explicit STORE
        public bool isStackStore() => ((addlflags & addl_flags.stack_store) != 0);

        /// Is always an input, even if unused
        public bool isLockedInput() => ((addlflags & addl_flags.locked_input) != 0);

        /// Is data-type propagation stopped
        public bool stopsUpPropagation() => ((addlflags & addl_flags.stop_uppropagation) != 0);

        /// Does \b this have an implied field
        public bool hasImpliedField() => ((addlflags & addl_flags.has_implied_field) != 0);

        /// Is \b this just a special placeholder representing INDIRECT creation?
        public bool isIndirectZero()
            => ((flags & (varnode_flags.indirect_creation | varnode_flags.constant)) == (varnode_flags.indirect_creation | Varnode.varnode_flags.constant));

        /// Is this Varnode \b created indirectly by a CALL operation?
        public bool isExtraOut()
            => ((flags & (varnode_flags.indirect_creation | varnode_flags.addrtied)) == varnode_flags.indirect_creation);

        /// Is \b this the low portion of a double precision value?
        public bool isPrecisLo() => ((flags & varnode_flags.precislo) != 0);

        /// Is \b this the high portion of a double precision value?
        public bool isPrecisHi() => ((flags & varnode_flags.precishi) != 0);

        /// Does this varnode get copied as a side-effect
        public bool isIncidentalCopy() => ((flags & varnode_flags.incidental_copy) != 0);

        /// Is \b this (not) considered a true write location when calculating SSA form?
        public bool isWriteMask() => ((addlflags & addl_flags.writemask) != 0);

        /// Must \b this be printed as unsigned
        public bool isUnsignedPrint() => ((addlflags & addl_flags.unsignedprint) != 0);

        /// Must \b this be printed as a \e long token
        public bool isLongPrint() => ((addlflags & addl_flags.longprint) != 0);

        /// Does \b this have a defining write operation?
        public bool isWritten() => ((flags & varnode_flags.written) != 0);

        /// Does \b this have Cover information?
        public bool hasCover()
            => ((flags & (varnode_flags.constant | varnode_flags.annotation | varnode_flags.insert)) == varnode_flags.insert);

        /// Return \b true if nothing reads this Varnode
        public bool hasNoDescend() => descend.empty();

        /// Return \b true if \b this is a constant with value \b val
        public bool constantMatch(ulong val)
        {
            return isConstant() && (loc.getOffset() == val);
        }

        /// Is \b this an (extended) constant
        /// If \b this is a constant, or is extended (INT_ZEXT,INT_SEXT) from a constant,
        /// the \e value of the constant is passed back and a non-negative integer is returned, either:
        ///   - 0 for a normal constant Varnode
        ///   - 1 for a zero extension (INT_ZEXT) of a normal constant
        ///   - 2 for a sign extension (INT_SEXT) of a normal constant
        /// \param val is a reference to the constant value that is passed back
        /// \return the extension code (or -1 if \b this cannot be interpreted as a constant)
        public int isConstantExtended(ulong val)
        {
            if (isConstant()) {
                val = getOffset();
                return 0;
            }
            if (!isWritten()) return -1;
            OpCode opc = def.code();
            if (opc == OpCode.CPUI_INT_ZEXT) {
                Varnode vn0 = def.getIn(0);
                if (vn0.isConstant()) {
                    val = vn0.getOffset();
                    return 1;
                }
            }
            else if (opc == OpCode.CPUI_INT_SEXT) {
                Varnode vn0 = def.getIn(0);
                if (vn0.isConstant()) {
                    val = vn0.getOffset();
                    return 2;
                }
            }
            return -1;
        }

        /// Return \b true if this Varnode is linked into the SSA tree
        public bool isHeritageKnown()
            => ((flags & (varnode_flags.insert | varnode_flags.constant | varnode_flags.annotation)) != 0);

        /// Does \b this have a locked Datatype?
        public bool isTypeLock() => ((flags & varnode_flags.typelock) != 0);

        /// Does \b this have a locked name?
        public bool isNameLock() => ((flags & varnode_flags.namelock) != 0);

        /// Mark \b this as currently being linked into the SSA tree
        public void setActiveHeritage()
        {
            addlflags |= addl_flags.activeheritage;
        }

        /// Mark \b this as not (actively) being linked into the SSA tree
        public void clearActiveHeritage()
        {
            addlflags &= ~addl_flags.activeheritage;
        }

        /// Mark this Varnode for breadcrumb algorithms
        public void setMark()
        {
            flags |= varnode_flags.mark;
        }

        /// Clear the mark on this Varnode
        public void clearMark() 
        {
            flags &= ~varnode_flags.mark;
        }

        /// Mark \b this as directly affected by a legal input
        public void setDirectWrite()
        {
            flags |= varnode_flags.directwrite;
        }

        /// Mark \b this as not directly affected by a legal input
        public void clearDirectWrite()
        {
            flags &= ~varnode_flags.directwrite;
        }

        /// Mark as forcing a value into \b this particular storage location
        public void setAddrForce()
        {
            setFlags(varnode_flags.addrforce);
        }

        /// Clear the forcing attribute
        public void clearAddrForce()
        {
            clearFlags(varnode_flags.addrforce);
        }

        /// Mark \b this as an \e implied variable in the final C source
        public void setImplied()
        {
            setFlags(varnode_flags.implied);
        }

        /// Clear the \e implied mark on this Varnode
        public void clearImplied()
        {
            clearFlags(varnode_flags.implied);
        }

        /// Mark \b this as an \e explicit variable in the final C source
        public void setExplicit()
        {
            setFlags(varnode_flags.explict);
        }

        /// Clear the \e explicit mark on this Varnode
        public void clearExplicit()
        {
            clearFlags(varnode_flags.explict);
        }

        /// Mark as storage location for a return address
        public void setReturnAddress()
        {
            flags |= varnode_flags.return_address;
        }

        /// Clear return address attribute
        public void clearReturnAddress()
        {
            flags &= ~varnode_flags.return_address;
        }

        /// Set \b this as checked for a constant symbol reference
        public void setPtrCheck()
        {
            addlflags |= addl_flags.ptrcheck;
        }

        /// Clear the pointer check mark on this Varnode
        public void clearPtrCheck()
        {
            addlflags &= ~addl_flags.ptrcheck;
        }

        /// Set \b this as flowing to or from pointer
        public void setPtrFlow()
        {
            addlflags |= addl_flags.ptrflow;
        }

        /// Indicate that this varnode is not flowing to or from pointer
        public void clearPtrFlow()
        {
            addlflags &= ~addl_flags.ptrflow;
        }

        /// Mark \b this as a special Varnode for tracking stackpointer values
        public void setSpacebasePlaceholder()
        {
            addlflags |= addl_flags.spacebase_placeholder;
        }

        /// Clear the stackpointer tracking mark
        public void clearSpacebasePlaceholder()
        {
            addlflags &= ~addl_flags.spacebase_placeholder;
        }

        /// Mark \b this as the low portion of a double precision value
        public void setPrecisLo()
        {
            setFlags(varnode_flags.precislo);
        }

        /// Clear the mark indicating a double precision portion
        public void clearPrecisLo()
        {
            clearFlags(varnode_flags.precislo);
        }

        /// Mark \b this as the high portion of a double precision value
        public void setPrecisHi()
        {
            setFlags(varnode_flags.precishi);
        }

        /// Clear the mark indicating a double precision portion
        public void clearPrecisHi()
        {
            clearFlags(varnode_flags.precishi);
        }

        /// Mark \b this as not a true \e write when computing SSA form
        public void setWriteMask()
        {
            addlflags |= addl_flags.writemask;
        }

        /// Clear the mark indicating \b this is not a true write
        public void clearWriteMask()
        {
            addlflags &= ~addl_flags.writemask;
        }

        /// Place temporary hold on dead code removal
        public void setAutoLiveHold()
        {
            flags |= varnode_flags.autolive_hold;
        }

        /// Clear temporary hold on dead code removal
        public void clearAutoLiveHold()
        {
            flags &= ~varnode_flags.autolive_hold;
        }

        /// Mark \b this gets pieced into larger structure
        public void setProtoPartial()
        {
            flags |= varnode_flags.proto_partial;
        }

        /// Clear mark indicating \b this gets pieced into larger structure
        public void clearProtoPartial()
        {
            flags &= ~varnode_flags.proto_partial;
        }

        /// Force \b this to be printed as unsigned
        public void setUnsignedPrint()
        {
            addlflags |= addl_flags.unsignedprint;
        }

        /// Force \b this to be printed as a \e long token
        public void setLongPrint()
        {
            addlflags |= addl_flags.longprint;
        }

        /// Stop up-propagation thru \b this
        public void setStopUpPropagation()
        {
            addlflags |= addl_flags.stop_uppropagation;
        }

        /// Stop up-propagation thru \b this
        public void clearStopUpPropagation()
        {
            addlflags &= ~addl_flags.stop_uppropagation;
        }

        /// Mark \b this as having an implied field
        public void setImpliedField()
        {
            addlflags |= addl_flags.has_implied_field;
        }

        /// (Possibly) set the Datatype given various restrictions
        /// Change the Datatype and lock state associated with this Varnode if various conditions are met
        ///    - Don't change a previously locked Datatype (unless \b override flag is \b true)
        ///    - Don't consider an \b undefined type to be locked
        ///    - Don't change to an identical Datatype
        /// \param ct is the Datatype to change to
        /// \param lock is \b true if the new Datatype should be locked
        /// \param override is \b true if an old lock should be overridden
        /// \return \b true if the Datatype or the lock setting was changed
        public bool updateType(Datatype ct, bool @lock, bool @override)
        {
            if (ct.getMetatype() == type_metatype.TYPE_UNKNOWN) // Unknown data type is ALWAYS unlocked
                @lock = false;

            if (isTypeLock() && (!@override)) return false; // Type is locked
            if ((type == ct) && (isTypeLock() == @lock)) return false; // No change
            flags &= ~varnode_flags.typelock;
            if (@lock)
                flags |= varnode_flags.typelock;
            type = ct;
            if (high != (HighVariable)null)
                high.typeDirty();
            return true;
        }

        /// Mark as produced by explicit OpCode.CPUI_STORE
        public void setStackStore()
        {
            addlflags |= addl_flags.stack_store;
        }

        /// Mark as existing input, even if unused
        public void setLockedInput()
        {
            addlflags |= addl_flags.locked_input;
        }

        /// Copy symbol info from \b vn
        /// Copy any symbol and type information from -vn- into this
        /// \param vn is the Varnode to copy from
        public void copySymbol(Varnode vn)
        {
            type = vn.type;        // Copy any type
            mapentry = vn.mapentry;    // Copy any symbol
            flags &= ~(varnode_flags.typelock | varnode_flags.namelock);
            flags |= (varnode_flags.typelock | varnode_flags.namelock) & vn.flags;
            if (high != (HighVariable)null) {
                high.typeDirty();
                if (mapentry != (SymbolEntry)null)
                    high.setSymbol(this);
            }
        }

        /// Copy symbol info from \b vn if constant value matches
        /// Symbol information (if present) is copied from the given constant Varnode into \b this,
        /// which also must be constant, but only if the two constants are \e close in the sense of an equate.
        /// \param vn is the given constant Varnode
        public void copySymbolIfValid(Varnode vn)
        {
            SymbolEntry? mapEntry = vn.getSymbolEntry();
            if (mapEntry == (SymbolEntry)null)
                return;
            EquateSymbol? sym = mapEntry.getSymbol() as EquateSymbol;
            if (sym == (EquateSymbol)null)
                return;
            if (sym.isValueClose(loc.getOffset(), size))
            {
                copySymbol(vn); // Propagate the markup into our new constant
            }
        }

        /// Calculate type of Varnode based on local information
        /// Make an initial determination of the Datatype of this Varnode. If a Datatype is already
        /// set and locked return it. Otherwise look through all the read PcodeOps and the write PcodeOp
        /// to determine if the Varnode is getting used as an \b int, \b float, or \b pointer, etc.
        /// Throw an exception if no Datatype can be found at all.
        /// \return the determined Datatype
        public Datatype getLocalType(bool blockup)
        {
            Datatype newct;

            if (isTypeLock())           // Our type is locked, don't change
                return type;        // Not a partial lock, return the locked type

            Datatype? ct = (Datatype)null;
            if (def != (PcodeOp)null) {
                ct = def.outputTypeLocal();
                if (def.stopsTypePropagation()) {
                    blockup = true;
                    return ct;
                }
            }

            int i;
            foreach (PcodeOp op in descend.GetEnumerator()) {
                i = op.getSlot(this);
                newct = op.inputTypeLocal(i);

                if (ct == (Datatype)null)
                    ct = newct;
                else {
                    if (0 > newct.typeOrder(ct))
                        ct = newct;
                }
            }
            if (ct == (Datatype)null)
                throw new LowlevelError("NULL local type");
            return ct;
        }

        /// Does \b this Varnode hold a formal boolean value
        /// If \b this varnode is produced by an operation with a boolean output, or if it is
        /// formally marked with a boolean data-type, return \b true. The parameter \b trustAnnotation
        /// toggles whether or not the formal data-type is trusted.
        /// \return \b true if \b this is a formal boolean, \b false otherwise
        public bool isBooleanValue(bool useAnnotation)
        {
            if (isWritten()) return def.isCalculatedBool();
            if (!useAnnotation)
                return false;
            if ((flags & (varnode_flags.input | varnode_flags.typelock)) == (varnode_flags.input | varnode_flags.typelock)) {
                if (size == 1 && type.getMetatype() == type_metatype.TYPE_BOOL)
                    return true;
            }
            return false;
        }

        /// Are \b this and \b op2 copied from the same source?
        /// Make a local determination if \b this and \b op2 hold the same value. We check if
        /// there is a common ancester for which both \b this and \b op2 are created from a direct
        /// sequence of COPY operations. NOTE: This is a transitive relationship
        /// \param op2 is the Varnode to compare to \b this
        /// \return \b true if the Varnodes are copied from a common ancestor
        public bool copyShadow(Varnode op2)
        {
            Varnode vn;

            if (this == op2) return true;
            // Trace -this- to the source of the copy chain
            vn = this;
            while ((vn.isWritten()) && (vn.getDef().code() == OpCode.CPUI_COPY)) {
                vn = vn.getDef().getIn(0);
                if (vn == op2) return true; // If we hit op2 then this and op2 must be the same
            }
            // Trace op2 to the source of copy chain
            while ((op2.isWritten()) && (op2.getDef().code() == OpCode.CPUI_COPY)) {
                op2 = op2.getDef().getIn(0);
                if (vn == op2) return true; // If the source is the same then this and op2 are same
            }
            return false;
        }

        /// \brief Try to find a SUBPIECE operation producing the value in \b this from the given \b whole Varnode
        ///
        /// The amount of truncation producing \b this must be known apriori. Allow for COPY and MULTIEQUAL operations
        /// in the flow path from \b whole to \b this.  This method will search recursively through branches
        /// of MULTIEQUAL up to a maximum depth.
        /// \param leastByte is the number of least significant bytes being truncated from \b whole to get \b this
        /// \param whole is the given whole Varnode
        /// \param recurse is the current depth of recursion
        /// \return \b true if \b this and \b whole have the prescribed SUBPIECE relationship
        public bool findSubpieceShadow(int leastByte, Varnode whole, int recurse)
        {
            Varnode vn = this;
            while (vn.isWritten() && vn.getDef().code() == OpCode.CPUI_COPY)
                vn = vn.getDef().getIn(0);
            if (!vn.isWritten()) {
                if (vn.isConstant()) {
                    while (whole.isWritten() && whole.getDef().code() == OpCode.CPUI_COPY)
                        whole = whole.getDef().getIn(0);
                    if (!whole.isConstant()) return false;
                    ulong off = whole.getOffset() >> leastByte * 8;
                    off &= Globals.calc_mask(vn.getSize());
                    return (off == vn.getOffset());
                }
                return false;
            }
            OpCode opc = vn.getDef().code();
            if (opc == OpCode.CPUI_SUBPIECE) {
                Varnode tmpvn = vn.getDef().getIn(0);
                int off = (int)vn.getDef().getIn(1).getOffset();
                if (off != leastByte || tmpvn.getSize() != whole.getSize())
                    return false;
                if (tmpvn == whole) return true;
                while (tmpvn.isWritten() && tmpvn.getDef().code() == OpCode.CPUI_COPY) {
                    tmpvn = tmpvn.getDef().getIn(0);
                    if (tmpvn == whole) return true;
                }
            }
            else if (opc == OpCode.CPUI_MULTIEQUAL) {
                recurse += 1;
                if (recurse > 1) return false;  // Truncate the recursion at maximum depth
                while (whole.isWritten() && whole.getDef().code() == OpCode.CPUI_COPY)
                    whole = whole.getDef().getIn(0);
                if (!whole.isWritten()) return false;
                PcodeOp bigOp = whole.getDef();
                if (bigOp.code() != OpCode.CPUI_MULTIEQUAL) return false;
                PcodeOp smallOp = vn.getDef();
                if (bigOp.getParent() != smallOp.getParent()) return false;
                // Recurse search through all branches of the two MULTIEQUALs
                for (int i = 0; i < smallOp.numInput(); ++i) {
                    if (!smallOp.getIn(i).findSubpieceShadow(leastByte, bigOp.getIn(i), recurse))
                        return false;
                }
                return true;    // All branches were copy shadows
            }
            return false;
        }

        /// \brief Try to find a PIECE operation that produces \b this from a given Varnode \b piece
        ///
        /// \param leastByte is the number of least significant bytes being truncated from the
        /// putative \b this to get \b piece.  The routine can backtrack through COPY operations and
        /// more than one PIECE operations to verify that \b this is formed out of \b piece.
        /// \param piece is the given Varnode piece
        /// \return \b true if \b this and \b whole have the prescribed PIECE relationship
        public bool findPieceShadow(int leastByte, Varnode piece)
        {
            Varnode vn = this;
            while (vn.isWritten() && vn.getDef().code() == OpCode.CPUI_COPY)
                vn = vn.getDef().getIn(0);
            if (!vn.isWritten()) return false;
            OpCode opc = vn.getDef().code();
            if (opc == OpCode.CPUI_PIECE) {
                Varnode tmpvn = vn.getDef().getIn(1);  // Least significant part
                if (leastByte >= tmpvn.getSize()) {
                    leastByte -= tmpvn.getSize();
                    tmpvn = vn.getDef().getIn(0);
                }
                else {
                    if (piece.getSize() + leastByte > tmpvn.getSize()) return false;
                }
                if (leastByte == 0 && tmpvn.getSize() == piece.getSize()) {
                    if (tmpvn == piece) return true;
                    while (tmpvn.isWritten() && tmpvn.getDef().code() == OpCode.CPUI_COPY) {
                        tmpvn = tmpvn.getDef().getIn(0);
                        if (tmpvn == piece) return true;
                    }
                    return false;
                }
                // OpCode.CPUI_PIECE input is too big, recursively search for another OpCode.CPUI_PIECE
                return tmpvn.findPieceShadow(leastByte, piece);
            }
            return false;
        }

        /// Is one of \b this or \b op2 a partial copy of the other?
        /// For \b this and another Varnode, establish that either:
        ///  - bigger = CONCAT(smaller,..) or
        ///  - smaller = SUBPIECE(bigger)
        ///
        /// Check through COPY chains and verify that the form of the CONCAT or SUBPIECE matches
        /// a given relative offset between the Varnodes.
        /// \param op2 is the Varnode to compare to \b this
        /// \param relOff is the putative relative byte offset of \b this to \b op2
        /// \return \b true if one Varnode is contained, as a value, in the other
        public bool partialCopyShadow(Varnode op2, int relOff)
        {
            Varnode vn;

            if (size < op2.size) {
                vn = this;
            }
            else if (size > op2.size) {
                vn = op2;
                op2 = this;
                relOff = -relOff;
            }
            else
                return false;
            if (relOff < 0)
                return false;       // Not proper containment
            if (relOff + vn.getSize() > op2.getSize())
                return false;       // Not proper containment

            bool bigEndian = getSpace().isBigEndian();
            int leastByte = bigEndian ? (op2.getSize() - vn.getSize()) - relOff : relOff;
            if (vn.findSubpieceShadow(leastByte, op2, 0))
                return true;

            if (op2.findPieceShadow(leastByte, vn))
                return true;

            return false;
        }

        /// Get structure/array/union that \b this is a piece of
        /// If \b this has a data-type built out of separate pieces, return it.
        /// If \b this is mapped as a partial to a symbol with one of these data-types, return it.
        /// Return null otherwise.
        /// \return the associated structured data-type or null
        public Datatype? getStructuredType()
        {
            Datatype ct;
            if (mapentry != (SymbolEntry)null)
                ct = mapentry.getSymbol().getType();
            else
                ct = type;
            if (ct.isPieceStructured())
                return ct;
            return (Datatype)null;
        }

        /// Encode a description of \b this to a stream
        /// Encode \b this as an \<addr> element, with at least the following attributes:
        ///   - \b space describes the AddrSpace
        ///   - \b offset of the Varnode within the space
        ///   - \b size of the Varnode is bytes
        ///
        /// Additionally the element will contain other optional attributes.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_ADDR);
            loc.getSpace().encodeAttributes(encoder, loc.getOffset(), size);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_REF, getCreateIndex());
            if (mergegroup != 0)
                encoder.writeSignedInteger(AttributeId.ATTRIB_GRP, getMergeGroup());
            if (isPersist())
                encoder.writeBool(AttributeId.ATTRIB_PERSISTS, true);
            if (isAddrTied())
                encoder.writeBool(AttributeId.ATTRIB_ADDRTIED, true);
            if (isUnaffected())
                encoder.writeBool(AttributeId.ATTRIB_UNAFF, true);
            if (isInput())
                encoder.writeBool(AttributeId.ATTRIB_INPUT, true);
            if (isVolatile())
                encoder.writeBool(AttributeId.ATTRIB_VOLATILE, true);
            encoder.closeElement(ElementId.ELEM_ADDR);
        }

        /// Compare Varnodes as pointers
        public static bool comparePointers(Varnode a, Varnode b)
        {
            throw new NotImplementedException();
            return (*a < *b);
        }

        /// Print raw info about a Varnode to stream
        /// Invoke the printRaw method on the given Varnode pointer, but take into account that it
        /// might be null.
        /// \param s is the output stream to write to
        /// \param vn is the given Varnode pointer (may be null)
        public static void printRaw(TextWriter s, Varnode? vn)
        {
            if (vn == (Varnode)null) {
                s.Write("<null>");
                return;
            }
            vn.printRaw(s);
        }
    }
}
