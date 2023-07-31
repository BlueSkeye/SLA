/* ###
 * IP: GHIDRA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
//#include "space.hh"
//#include "translate.hh"

using Sla;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Text;
using System.Windows.Markup;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.CORE {
    /// \brief A region where processor data is stored
    /// An AddrSpace (Address Space) is an arbitrary sequence of bytes where a processor
    /// can store data. As is usual with most processors' concept of RAM, an integer
    /// offset paired with an AddrSpace forms the address (See Address) of a byte.
    /// addressed and is usually described by the number of bytes needed to encode the
    /// biggest offset.  I.e. a \e 4-byte address space means that there are offsets
    /// ranging from 0x00000000 to 0xffffffff within the space
    /// for a total of 2^32 addressable bytes within the space.
    /// There can be multiple address spaces, and it is typical to have spaces
    ///     - \b ram        Modeling the main processor address bus
    ///     - \b register   Modeling a processors registers
    /// The processor specification can set up any address spaces it needs in an
    ///  arbitrary manner, but \e all data manipulated by the processor, which the 
    /// specification hopes to model, must be contained in some address space, including
    /// RAM, ROM, general registers, special registers, i/o ports, etc.
    ///
    /// The analysis engine also uses additional address spaces to
    /// model special concepts.  These include
    ///     - \b const   There is a \e constant address space for modeling constant
    ///                  values in p-code expressions (See ConstantSpace)
    ///     - \b unique  There is always a \e unique address space used as a pool for
    ///                  temporary registers. (See UniqueSpace)
    public class AddrSpace
    {
        internal static readonly AddrSpace MaxAddressSpace = new AddrSpace();

        // Space container
        // friend class AddrSpaceManager;
        [Flags()]
        public enum Properties {
            /// Space is big endian if set, little endian otherwise
            big_endian = 1,
            /// This space is heritaged
            heritaged = 2,
            /// Dead-code analysis is done on this space
            does_deadcode = 4,
            /// Space is specific to a particular loadimage
            programspecific = 8,
            /// Justification within aligned word is opposite of endianness
            reverse_justification = 16,
            /// Space attached to the formal \b stack \b pointer
            formal_stackspace = 0x20,
            /// This space is an overlay of another space
            overlay = 0x40,
            /// This is the base space for overlay space(s)
            overlaybase = 0x80,
            /// Space is truncated from its original size, expect pointers larger than this size
            truncated = 0x100,
            /// Has physical memory associated with it
            hasphysical = 0x200,
            /// Quick check for the OtherSpace derived class
            is_otherspace = 0x400,
            /// Does there exist near pointers into this space
            has_nearpointers = 0x800
        }

        /// Type of space (PROCESSOR, CONSTANT, INTERNAL, ...)
        internal spacetype type;
        /// Manager for processor using this space
        internal AddrSpaceManager manage;
        /// Processor translator (for register names etc) for this space
        internal readonly Translate trans;
        /// Number of managers using this space
        internal int refcount;
        /// Attributes of the space
        internal Properties flags;
        /// Highest (byte) offset into this space
        internal ulong highest;
        /// Offset below which we don't search for pointers
        internal ulong pointerLowerBound;
        /// Offset above which we don't search for pointers
        internal ulong pointerUpperBound;
        /// Shortcut character for printing
        internal char shortcut;

        /// Name of this space
        internal string? name;
        /// Size of an address into this space in bytes
        internal uint addressSize;
        /// Size of unit being addressed (1=byte)
        internal uint wordsize;
        /// Smallest size of a pointer into \b this space (in bytes)
        internal int minimumPointerSize;
        /// An integer identifier for the space
        internal int index;
        /// Delay in heritaging this space
        internal int delay;
        /// Delay before deadcode removal is allowed on this space
        internal int deadcodedelay;

        /// <summary>For exclusive use of MaxAddressSpace instanciation</summary>
        private AddrSpace()
        {
        }

        public virtual void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        internal bool IsMaxAddressSpace => object.ReferenceEquals(this, MaxAddressSpace);

        internal virtual bool IsConstantSpace => false;

        /// Calculate scale and mask
        /// Calculate \e highest based on \e addressSize, and \e wordsize.
        /// This also calculates the default pointerLowerBound
        internal void calcScaleMask()
        {
            pointerLowerBound = (addressSize < 3) ? 0x100UL : 0x1000UL;
            // Maximum address
            highest = Globals.calc_mask(addressSize);
            // Maximum byte address
            highest = highest * wordsize + (wordsize - 1);
            pointerUpperBound = highest;
        }

        /// Set a cached attribute
        /// An internal method for derived classes to set space attributes
        /// \param fl is the set of attributes to be set
        internal void setFlags(Properties fl)
        {
            flags |= fl;
        }

        /// Clear a cached attribute
        /// An internal method for derived classes to clear space attibutes
        /// \param fl is the set of attributes to clear
        internal void clearFlags(Properties fl)
        {
            flags &= ~fl;
        }

        /// Write the XML attributes of this space
        /// Save the \e name, \e index, \e bigendian, \e delay,
        /// \e size, \e wordsize, and \e physical attributes which
        /// are common with all address spaces derived from AddrSpace
        /// \param s the stream where the attributes are written
        internal void saveBasicAttributes(StreamWriter s)
        {
            Xml.a_v(s, "name", name);
            Xml.a_v_i(s, "index", index);
            Xml.a_v_b(s, "bigendian", isBigEndian());
            Xml.a_v_i(s, "delay", delay);
            if (delay != deadcodedelay) {
                Xml.a_v_i(s, "deadcodedelay", deadcodedelay);
            }
            Xml.a_v_i(s, "size", addressSize);
            if (wordsize > 1) {
                Xml.a_v_i(s, "wordsize", wordsize);
            }
            Xml.a_v_b(s, "physical", hasPhysical());
        }

        /// Read attributes for \b this space from an open XML element
        /// Walk attributes of the current element and recover all the properties defining
        /// this space.  The processor translator, \e trans, and the
        /// \e type must already be filled @in.
        /// \param decoder is the stream decoder
        internal void decodeBasicAttributes(Decoder decoder)
        {
            deadcodedelay = -1;
            for (; ; ) {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_NAME) {
                    name = decoder.readString();
                }
                if (attribId == AttributeId.ATTRIB_INDEX) {
                    index = (int)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_SIZE) {
                    addressSize = (uint)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_WORDSIZE) {
                    wordsize = (uint)decoder.readUnsignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_BIGENDIAN) {
                    if (decoder.readBool()) {
                        flags |= Properties.big_endian;
                    }
                }
                else if (attribId == AttributeId.ATTRIB_DELAY) {
                    delay = (int)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_DEADCODEDELAY) {
                    deadcodedelay = (int)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_PHYSICAL) {
                    if (decoder.readBool()) {
                        flags |= Properties.hasphysical;
                    }
                }
            }
            if (deadcodedelay == -1) {
                // If deadcodedelay attribute not present, set it to delay
                deadcodedelay = delay;
            }
            calcScaleMask();
        }

        /// The logical form of the space is truncated from its actual size
        /// Pointers may refer to this original size put the most significant bytes are ignored
        /// \param newsize is the size (in bytes) of the truncated (logical) space
        internal void truncateSpace(uint newsize)
        {
            setFlags(Properties.truncated);
            addressSize = newsize;
            minimumPointerSize = (int)newsize;
            calcScaleMask();
        }

        /// Initialize an address space with its basic attributes
        /// \param m is the space manager associated with the new space
        /// \param t is the processor translator associated with the new space
        /// \param tp is the type of the new space (PROCESSOR, CONSTANT, INTERNAL,...)
        /// \param nm is the name of the new space
        /// \param size is the (offset encoding) size of the new space
        /// \param ws is the number of bytes in an addressable unit
        /// \param ind is the integer identifier for the new space
        /// \param fl can be 0 or AddrSpace::hasphysical
        /// \param dl is the number of rounds to delay heritage for the new space
        public AddrSpace(AddrSpaceManager m, Translate t, spacetype tp, string nm, uint size,
            uint ws, int ind, Properties fl, int dl)
        {
            // No references to this space yet
            refcount = 0;
            manage = m;
            trans = t;
            type = tp;
            name = nm;
            addressSize = size;
            wordsize = ws;
            index = ind;
            delay = dl;
            // Deadcode delay initially starts the same as heritage delay
            deadcodedelay = dl;
            // (initially) assume pointers must match the space size exactly
            minimumPointerSize = 0;
            // Placeholder meaning shortcut is unassigned
            shortcut = ' ';

            // These are the flags we allow to be set from constructor
            flags = (fl & Properties.hasphysical);
            if (t.isBigEndian()) {
                flags |= Properties.big_endian;
            }
            // Always on unless explicitly turned off in derived constructor
            flags |= (Properties.heritaged | Properties.does_deadcode);
            calcScaleMask();
        }

        ///< For use with decode
        /// This is a partial constructor, for initializing a space
        /// via XML
        /// \param m the associated address space manager
        /// \param t is the processor translator
        /// \param tp the basic type of the space
        public AddrSpace(AddrSpaceManager m, Translate t, spacetype tp)
        {
            refcount = 0;
            manage = m;
            trans = t;
            type = tp;
            // Always on unless explicitly turned off in derived constructor
            flags = (Properties.heritaged | Properties.does_deadcode);
            wordsize = 1;
            minimumPointerSize = 0;
            shortcut = ' ';
            // We let big_endian get set by attribute
        }

        ///< The address space destructor
        ~AddrSpace()
        {
        }

        ///< Get the name
        /// Every address space has a (unique) name, which is referred
        /// to especially in configuration files via XML.
        /// \return the name of this space
        public string getName()
        {
            return name;
        }

        ///< Get the space manager
        /// Every address space is associated with a manager of (all possible) spaces.
        /// This method recovers the address space manager object.
        /// \return a pointer to the address space manager
        public AddrSpaceManager getManager()
        {
            return manage;
        }

        ///< Get the processor translator
        /// Every address space is associated with a processor which may have additional objects
        /// like registers etc. associated with it. This method returns a pointer to that processor
        /// translator
        /// \return a pointer to the Translate object
        public Translate getTrans()
        {
            return trans;
        }

        ///< Get the type of space
        /// Return the defining type for this address space.
        ///   - IPTR_CONSTANT for the constant space
        ///   - IPTR_PROCESSOR for a normal space
        ///   - IPTR_INTERNAL for the temporary register space
        ///   - IPTR_FSPEC for special FuncCallSpecs references
        ///   - IPTR_IOP for special PcodeOp references
        /// \return the basic type of this space
        public spacetype getType()
        {
            return type;
        }

        ///< Get number of heritage passes being delayed
        /// If the heritage algorithms need to trace dataflow
        /// within this space, the algorithms can delay tracing this
        /// space in order to let indirect references into the space
        /// resolve themselves.  This method indicates the number of
        /// rounds of dataflow analysis that should be skipped for this
        /// space to let this resolution happen
        /// \return the number of rounds to skip heritage
        public int getDelay()
        {
            return delay;
        }

        ///< Get number of passes before deadcode removal is allowed
        /// The point at which deadcode removal is performed on varnodes within
        /// a space can be set to skip some number of heritage passes, in case
        /// not all the varnodes are created within a single pass. This method
        /// gives the number of rounds that should be skipped before deadcode
        /// elimination begins
        /// \return the number of rounds to skip deadcode removal
        public int getDeadcodeDelay()
        {
            return deadcodedelay;
        }

        ///< Get the integer identifier
        /// Each address space has an associated index that can be used
        /// as an integer encoding of the space.
        /// \return the unique index
        public int getIndex()
        {
            return index;
        }

        ///< Get the addressable unit size
        /// This method indicates the number of bytes contained in an
        /// \e addressable \e unit of this space.  This is almost always
        /// 1, but can be any other small integer.
        /// \return the number of bytes in a unit
        public uint getWordSize()
        {
            return wordsize;
        }

        ///< Get the size of the space
        /// Return the number of bytes needed to represent an offset
        /// into this space.  A space with 2^32 bytes has an address
        /// size of 4, for instance.
        /// \return the size of an address
        public uint getAddrSize()
        {
            return addressSize;
        }

        ///< Get the highest byte-scaled address
        /// Get the highest (byte) offset possible for this space
        /// \return the offset
        public ulong getHighest()
        {
            return highest;
        }

        ///< Get lower bound for assuming an offset is a pointer
        /// Constant offsets are tested against \b this lower bound as a quick filter before
        /// attempting to lookup symbols.
        /// \return the minimum offset that will be inferred as a pointer
        public ulong getPointerLowerBound()
        {
            return pointerLowerBound;
        }

        ///< Get upper bound for assuming an offset is a pointer
        /// Constant offsets are tested against \b this upper bound as a quick filter before
        /// attempting to lookup symbols.
        /// \return the maximum offset that will be inferred as a pointer
        public ulong getPointerUpperBound()
        {
            return pointerUpperBound;
        }

        ///< Get the minimum pointer size for \b this space
        /// A value of 0 means the size must match exactly. If the space is truncated, or
        /// if there exists near pointers, this value may be non-zero.
        public int getMinimumPtrSize()
        {
            return minimumPointerSize;
        }

        ///< Wrap -off- to the offset that fits into this space
        /// Calculate \e off modulo the size of this address space in
        /// order to construct the offset "equivalent" to \e off that
        /// fits properly into this space
        /// \param off is the offset requested
        /// \return the wrapped offset
        public ulong wrapOffset(ulong off)
        {
            if (off <= highest)     // Comparison is unsigned
                return off;
            long mod = (long)(highest + 1);
            long res = (long)off % mod; // remainder is signed
            if (res < 0)            // Remainder may be negative
                res += mod;         // Adding mod guarantees res is in (0,mod)
            return (ulong)res;
        }

        ///< Get the shortcut character
        /// Return a unique short cut character that is associated
        /// with this space.  The shortcut character can be used by
        /// the read method to quickly specify the space of an address.
        /// \return the shortcut character
        public char getShortcut()
        {
            return shortcut;
        }

        ///< Return \b true if dataflow has been traced
        /// During analysis, memory locations in most spaces need to
        /// have their data-flow traced.  This method returns \b true
        /// for these spaces.  For some of the special spaces, like
        /// the \e constant space, tracing data flow makes no sense,
        /// and this routine will return \b false.
        /// \return \b true if this space's data-flow is analyzed
        public bool isHeritaged()
        {
            return ((flags & Properties.heritaged) != 0);
        }

        ///< Return \b true if dead code analysis should be done on this space
        /// Most memory locations should have dead-code analysis performed,
        /// and this routine will return \b true.
        /// For certain special spaces like the \e constant space, dead-code
        /// analysis doesn't make sense, and this routine returns \b false.
        public bool doesDeadcode()
        {
            return ((flags & Properties.does_deadcode) != 0);
        }

        ///< Return \b true if data is physically stored in this
        /// This routine returns \b true, if, like most spaces, the space
        /// has actual read/writeable bytes associated with it.
        /// Some spaces, like the \e constant space, do not.
        /// \return \b true if the space has physical data in it.
        public bool hasPhysical()
        {
            return ((flags & Properties.hasphysical) != 0);
        }

        ///< Return \b true if values in this space are big endian
        /// If integer values stored in this space are encoded in this
        /// space using the big endian format, then return \b true.
        /// \return \b true if the space is big endian
        public bool isBigEndian()
        {
            return ((flags & Properties.big_endian) != 0);
        }

        ///< Return \b true if alignment justification does not match endianness
        /// Certain architectures or compilers specify an alignment for accessing words within the space
        /// The space required for a variable must be rounded up to the alignment. For variables smaller
        /// than the alignment, there is the issue of how the variable is "justified" within the aligned
        /// word. Usually the justification depends on the endianness of the space, for certain weird
        /// cases the justification may be the opposite of the endianness.
        public bool isReverseJustified()
        {
            return ((flags & Properties.reverse_justification) != 0);
        }

        ///< Return \b true if \b this is attached to the formal \b stack \b pointer
        /// Currently an architecture can declare only one formal stack pointer.
        public bool isFormalStackSpace()
        {
            return ((flags & Properties.formal_stackspace) != 0);
        }

        ///< Return \b true if this is an overlay space
        public bool isOverlay()
        {
            return ((flags & Properties.overlay) != 0);
        }

        ///< Return \b true if other spaces overlay this space
        public bool isOverlayBase()
        {
            return ((flags & Properties.overlaybase) != 0);
        }

        ///< Return \b true if \b this is the \e other address space
        public bool isOtherSpace()
        {
            return ((flags & Properties.is_otherspace) != 0);
        }

        ///< Return \b true if this space is truncated from its original size
        /// If this method returns \b true, the logical form of this space is truncated from its actual size
        /// Pointers may refer to this original size put the most significant bytes are ignored
        public bool isTruncated()
        {
            return ((flags & Properties.truncated) != 0);
        }

        ///< Return \b true if \e near (truncated) pointers into \b this space are possible
        public bool hasNearPointers()
        {
            return ((flags & Properties.has_nearpointers) != 0);
        }

        ///< Write an address offset to a stream
        /// Print the \e offset as hexidecimal digits.
        /// \param s is the stream to write to
        /// \param offset is the offset to be printed
        public void printOffset(StreamWriter s, ulong offset)
        {
            s.Write("0x{0:X}", offset);
        }

        ///< Number of base registers associated with this space
        /// Some spaces are "virtual", like the stack spaces, where addresses are really relative to a
        /// base pointer stored in a register, like the stackpointer.  This routine will return non-zero
        /// if \b this space is virtual and there is 1 (or more) associated pointer registers
        /// \return the number of base registers associated with this space
        public int numSpacebase()
        {
            return 0;
        }

        ///< Get a base register that creates this virtual space
        /// For virtual spaces, like the stack space, this routine returns the location information for
        /// a base register of the space.  This routine will throw an exception if the register does not exist
        /// \param i is the index of the base register starting at
        /// \return the VarnodeData that describes the register
        public ref VarnodeData getSpacebase(int i)
        {
            throw new LowlevelError(name + " space is not virtual and has no associated base register");
        }

        ///< Return original spacebase register before truncation
        /// If a stack pointer is truncated to fit the stack space, we may need to know the
        /// extent of the original register
        /// \param i is the index of the base register
        /// \return the original register before truncation
        public ref VarnodeData getSpacebaseFull(int i)
        {
            throw new LowlevelError(name + " has no truncated registers");
        }

        ///< Return \b true if a stack in this space grows negative
        /// For stack (or other spacebase) spaces, this routine returns \b true if the space can viewed as a stack
        /// and a \b push operation causes the spacebase pointer to be decreased (grow negative)
        /// \return \b true if stacks grow in negative direction.
        public virtual bool stackGrowsNegative()
        {
            return true;
        }

        ///< Return this space's containing space (if any)
        /// If this space is virtual, then
        /// this routine returns the containing address space, otherwise
        /// it returns NULL.
        /// \return a pointer to the containing space or NULL
        public virtual AddrSpace? getContain()
        {
            return null;
        }

        /// \brief Determine if a given point is contained in an address range in \b this address space
        /// The point is specified as an address space and offset pair plus an additional number of bytes to "skip".
        /// A non-negative value is returned if the point falls in the address range.
        /// If the point falls on the first byte of the range, 0 is returned. For the second byte, 1 is returned, etc.
        /// Otherwise -1 is returned.
        /// \param offset is the starting offset of the address range within \b this space
        /// \param size is the size of the address range in bytes
        /// \param pointSpace is the address space of the given point
        /// \param pointOff is the offset of the given point
        /// \param pointSkip is the additional bytes to skip
        /// \return a non-negative value indicating where the point falls in the range, or -1
        public virtual int overlapJoin(ulong offset, int size, AddrSpace pointSpace, ulong pointOff,
            int pointSkip)
        {
            if (this != pointSpace) {
                return -1;
            }
            if (0 > pointSkip) {
                throw new BugException();
            }
            ulong dist = wrapOffset(pointOff + (ulong)pointSkip - offset);
            // but must fall before op+size
            if (0 > size) {
                throw new BugException();
            }
            return (dist >= (ulong)size) ? -1: (int)dist;
        }

        ///< Encode address attributes to a stream
        /// Write the main attributes for an address within \b this space.
        /// The caller provides only the \e offset, and this routine fills
        /// in other details pertaining to this particular space.
        /// \param encoder is the stream encoder
        /// \param offset is the offset of the address
        public virtual void encodeAttributes(Encoder encoder, ulong offset)
        {
            encoder.writeSpace(AttributeId.ATTRIB_SPACE, this);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_OFFSET, offset);
        }

        ///< Encode an address and size attributes to a stream
        /// Write the main attributes of an address with \b this space
        /// and a size. The caller provides the \e offset and \e size,
        /// and other details about this particular space are filled @in.
        /// \param encoder is the stream encoder
        /// \param offset is the offset of the address
        /// \param size is the size of the memory location
        public virtual void encodeAttributes(Encoder encoder, ulong offset, int size)
        {
            encoder.writeSpace(AttributeId.ATTRIB_SPACE, this);
            encoder.writeUnsignedInteger(AttributeId.ATTRIB_OFFSET, offset);
            encoder.writeSignedInteger(AttributeId.ATTRIB_SIZE, size);
        }

        ///< Recover an offset and size
        /// For an open element describing an address in \b this space, this routine
        /// recovers the offset and possibly the size described by the element
        /// \param decoder is the stream decoder
        /// \param size is a reference where the recovered size should be stored
        /// \return the recovered offset
        public virtual ulong decodeAttributes(Decoder decoder, ref uint size)
        {
            ulong offset = 0;
            bool foundoffset = false;
            for (; ; ) {
                uint attribId = decoder.getNextAttributeId();
                if (0 == attribId) {
                    break;
                }
                if (attribId == AttributeId.ATTRIB_OFFSET) {
                    foundoffset = true;
                    offset = decoder.readUnsignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_SIZE) {
                    size = (uint)decoder.readSignedInteger();
                }
            }
            if (!foundoffset) {
                throw new LowlevelError("Address is missing offset");
            }
            return offset;
        }

        ///< Write an address in this space to a stream
        /// This is a printing method for the debugging routines. It
        /// prints taking into account the \e wordsize, adding a
        /// "+n" if the offset is not on-cut with wordsize. It also
        /// returns the expected/typical size of values from this space.
        /// \param s is the stream being written
        /// \param offset is the offset to be printed
        public virtual void printRaw(StreamWriter s, ulong offset)
        {
            int sz = (int)getAddrSize();
            if (sz > 4) {
                sz = ((offset >> 32) == 0)
                    ? 4 // Don't print a bunch of zeroes at front of address
                    : 6;
            }
            string formatString = $"0x{{0:X{2 * sz}}}";
            s.Write(formatString, byteToAddress(offset, wordsize));
            if (wordsize > 1) {
                int cut = (int)(offset % wordsize);
                if (cut != 0) {
                    s.Write("+{0:D}", cut);
                }
            }
        }

        ///< Read in an address (and possible size) from a string
        /// For the console mode, an address space can tailor how it
        /// converts user strings into offsets within the space. The
        /// base routine can read and convert register names as well
        /// as absolute hex addresses.  A size can be indicated by
        /// appending a ':' and integer, .i.e.  0x1000:2.  Offsets within
        /// a register can be indicated by appending a '+' and integer,
        /// i.e. eax+2
        /// \param s is the string to be parsed
        /// \param size is a reference to the size being returned
        /// \return the parsed offset
        public virtual unsafe ulong read(string s, out int size)
        {
            /*const*/
            char* enddata;
            char* tmpdata;
            int expsize;
            string frontpart;
            ulong offset;

            int append = s.IndexOf(":+");
            try {
                if (-1 == append) {
                    /*const*/
                    ref VarnodeData point = ref trans.getRegister(s);
                    offset = point.offset;
                    size = (int)(point.size);
                }
                else {
                    frontpart = s.Substring(0, append);
                    /*const*/
                    ref VarnodeData point = ref trans.getRegister(frontpart);
                    offset = point.offset;
                    size = (int)point.size;
                }
            }
            catch (LowlevelError) {
                // Name doesn't exist
                fixed(char* pString = s) {
                    offset = Globals.Strtoul(pString, out tmpdata, 0);
                    offset = addressToByte(offset, wordsize);
                    enddata = tmpdata;
                    if ((enddata - pString) == s.Length) {
                        // If no size or offset override
                        // Return "natural" size
                        size = (int)manage.getDefaultSize();
                        return offset;
                    }
                    size = (int)manage.getDefaultSize();
                }
            }
            if (-1 != append) {
                fixed (char* pString = s) {
                    enddata = pString + append;
                    expsize = Globals.get_offset_size(enddata, ref offset);
                }
                if (expsize != -1) {
                    size = expsize;
                    return offset;
                }
            }
            return offset;
        }

        /// Write the details of this space as XML
        /// Write a tag fully describing the details of this space
        /// suitable for later recovery via decode.
        /// \param s is the stream being written
        public virtual void saveXml(StreamWriter s)
        {
            // This implies type=processor
            s.Write("<space");
            saveBasicAttributes(s);
            s.WriteLine("/>");
        }

        /// Recover the details of this space from XML
        public virtual void decode(Decoder decoder)
        {
            // Multiple tags: <space>, <space_other>, <space_unique>
            uint elemId = decoder.openElement();
            decodeBasicAttributes(decoder);
            decoder.closeElement(elemId);
        }

        ///< Scale from addressable units to byte units
        /// Given an offset into an address space based on the addressable unit size (wordsize),
        /// convert it into a byte relative offset
        /// \param val is the offset to convert
        /// \param ws is the number of bytes in the addressable word
        /// \return the scaled offset
        public static ulong addressToByte(ulong val, uint ws)
        {
            return val * ws;
        }

        ///< Scale from byte units to addressable units
        /// Given an offset in an address space based on bytes, convert it
        /// into an offset relative to the addressable unit of the space (wordsize)
        /// \param val is the offset to convert
        /// \param ws is the number of bytes in the addressable word
        /// \return the scaled offset
        public static ulong byteToAddress(ulong val, uint ws)
        {
            return val / ws;
        }

        /// Scale int from addressable units to byte units
        /// Given an int offset into an address space based on the addressable unit size (wordsize),
        /// convert it into a byte relative offset
        /// \param val is the offset to convert
        /// \param ws is the number of bytes in the addressable word
        /// \return the scaled offset
        public static int addressToByteInt(int val, uint ws)
        {
            return (int)(val * ws);
        }

        /// Scale int from byte units to addressable units
        /// Given an int offset in an address space based on bytes, convert it
        /// into an offset relative to the addressable unit of the space (wordsize)
        /// \param val is the offset to convert
        /// \param ws is the number of bytes in the addressable word
        /// \return the scaled offset
        public static int byteToAddressInt(int val, uint ws)
        {
            return (int)(val / ws);
        }

        /// Compare two spaces by their index
        /// For sorting a sequence of address spaces.
        /// \param a is the first space
        /// \param b is the second space
        /// \return \b true if the first space should come before the second
        public static bool compareByIndex(AddrSpace a, AddrSpace b)
        {
            return (a.index < b.index);
        }
    }
}
