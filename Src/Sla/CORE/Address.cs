
namespace Sla.CORE
{
    /// \brief A low-level machine address for labelling bytes and data.
    /// All data that can be manipulated within the processor reverse engineering model
    /// can be labelled with an Address. It is simply an address space (AddrSpace) 
    /// and an offset within that space. Note that processor registers are typically
    /// modelled by creating a dedicated address space for them, as distinct from RAM say,
    /// and then specifying certain addresses within the register space that correspond 
    /// to particular registers. However, an arbitrary address could refer to anything,
    /// RAM, ROM, cpu register, data segment, coprocessor, stack, nvram, etc.
    /// An Address represents an offset \e only, not an offset and length
    public class Address : IComparable<Address>, IEquatable<Address>
    {
        /// Pointer to our address space
        internal AddrSpace? @base;
        /// Offset (in bytes)
        internal ulong offset;

        /// An enum for specifying extremal addresses
        public enum mach_extreme
        {
            /// Smallest possible address
            m_minimal,
            /// Biggest possible address
            m_maximal
        }

        /// Some data structures sort on an Address, and it is convenient
        /// to be able to create an Address that is either bigger than
        /// or smaller than all other Addresses.
        /// \param ex is either \e m_minimal or \e m_maximal
        /// Initialize an extremal address
        public Address(mach_extreme ex)
        {
            if (ex == mach_extreme.m_minimal) {
                @base = null;
                offset = 0;
            }
            else {
                @base = AddrSpace.MaxAddressSpace;
                offset = ulong.MaxValue;
            }
        }

        /// Create an invalid address
        /// An invalid address is possible in some circumstances.
        /// This deliberately constructs an invalid address
        public Address()
        {
            @base = null;
        }

        /// Construct an address with a space/offset pair
        /// This is the basic Address constructor
        /// \param id is the space containing the address
        /// \param off is the offset of the address
        public Address(AddrSpace id, ulong off)
        {
            @base = id;
            offset = off;
        }

        /// A copy constructor
        /// This is a standard copy constructor, copying the
        /// address space and the offset
        /// \param op2 is the Address to copy
        public Address(ref Address op2)
        {
            @base = op2.@base;
            offset = op2.offset;
        }

        /// Is the address invalid?
        /// Determine if this is an invalid address. This only
        /// detects \e deliberate invalid addresses.
        /// \return \b true if the address is invalid
        public bool isInvalid()
        {
            return (@base == null);
        }

        /// Get the number of bytes in the address
        /// Get the number of bytes needed to encode the \e offset
        /// for this address.
        /// \return the number of bytes in the encoding
        public int getAddrSize()
        {
            if (null == @base) {
                throw new BugException();
            }
            return (int)@base.getAddrSize();
        }

        /// Is data at this address big endian encoded
        /// Determine if data stored at this address is big endian encoded.
        /// \return \b true if the address is big endian
        public bool isBigEndian()
        {
            if (null == @base) {
                throw new BugException();
            }
            return @base.isBigEndian();
        }

        /// Write a raw version of the address to a stream
        /// Write a short-hand or debug version of this address to a stream.
        /// \param s is the stream being written
        public void printRaw(TextWriter s)
        {
            if (@base == null) {
                s.Write("invalid_addr");
                return;
            }
            @base.printRaw(s, offset);
        }

        /// Read in the address from a string
        /// Convert a string into an address. The string format can be
        /// tailored for the particular address space.
        /// \param s is the string to parse
        /// \return any size associated with the parsed string
        public int read(string s)
        {
            if (null == @base) {
                throw new BugException();
            }
            int sz;
            offset = @base.read(s, out sz);
            return sz;
        }

        /// Get the address space
        /// Get the address space associated with this address.
        /// \return the AddressSpace pointer, or \b NULL if invalid
        public AddrSpace getSpace()
        {
            if (null == @base) {
                throw new BugException();
            }
            return @base;
        }

        /// Get the offset of the address as an integer.
        /// \return the offset integer
        public ulong getOffset()
        {
            return offset;
        }

        ///< Get the shortcut character for the address space
        /// Each address has a shortcut character associated with it
        /// for use with the read and printRaw methods.
        /// \return the shortcut char
        public char getShortcut()
        {
            if (null == @base) {
                throw new BugException();
            }
            return @base.getShortcut();
        }

        // TODO : Find where the assignment operator is used. Reference copy must be
        // replaced with this Copy operator.
        // WARNING : There is a huge code base to scan for.
        /// Copy an address
        /// This is a standard assignment operator, copying the address space pointer
        /// and the offset
        /// \param op2 is the Address being assigned
        /// \return a reference to altered address
        public Address Copy(Address from)
        {
            this.@base = from.@base;
            this.offset = from.offset;
            return this;
        }

        /// Compare two addresses for equality
        /// Check if two addresses are equal. I.e. if their address
        /// space and offset are the same.
        /// \param op2 is the address to compare to \e this
        /// \return \b true if the addresses are the same
        public static bool operator ==(Address op1, Address op2)
        {
            return ((op1.@base == op2.@base) && (op1.offset == op2.offset));
        }

        public bool Equals(Address? other)
        {
            if (other == null) { return false; }
            return (other == this);
        }

        /// Compare two addresses for inequality
        /// Check if two addresses are not equal.  I.e. if either their
        /// address space or offset are different.
        /// \param op2 is the address to compare to \e this
        /// \return \b true if the addresses are different
        public static bool operator !=(Address op1, Address op2)
        {
            return !(op1 == op2);
        }

        /// Compare two addresses via their natural ordering
        /// Do an ordering comparison of two addresses.  Addresses are
        /// sorted first on space, then on offset.  So two addresses in
        /// the same space compare naturally based on their offset, but
        /// addresses in different spaces also compare. Different spaces
        /// are ordered by their index.
        /// \param op2 is the address to compare to
        /// \return \b true if \e this comes before \e op2
        public static bool operator <(Address op1, Address op2)
        {
            if (op1.@base != op2.@base) {
                if (op1.@base == null) {
                    return true;
                }
                if (op1.@base.IsMaxAddressSpace) {
                    return false;
                }
                if (op2.@base == null) {
                    return false;
                }
                if (op2.@base.IsMaxAddressSpace) {
                    return true;
                }
                return (op1.@base.getIndex() < op2.@base.getIndex());
            }
            if (op1.offset != op2.offset) {
                return (op1.offset < op2.offset);
            }
            return false;
        }

        public static bool operator >(Address op1, Address op2)
        {
            return !(op1 <= op2);
        }

        public static bool operator >=(Address op1, Address op2)
        {
            return !(op1 < op2);
        }

        /// Compare two addresses via their natural ordering
        /// Do an ordering comparison of two addresses.
        /// \param op2 is the address to compare to
        /// \return \b true if \e this comes before or is equal to \e op2
        public static bool operator <=(Address op1,  Address op2)
        {
            if (op1.@base != op2.@base) {
                if (op1.@base == null) {
                    return true;
                }
                if (op1.@base.IsMaxAddressSpace) {
                    return false;
                }
                if (null == op2.@base) {
                    return false;
                }
                if (op2.@base.IsMaxAddressSpace) {
                    return true;
                }
                return (op1.@base.getIndex() < op2.@base.getIndex());
            }
            if (op1.offset != op2.offset) {
                return (op1.offset < op2.offset);
            }
            return true;
        }

        /// Increment address by a number of bytes
        /// Add an integer value to the offset portion of the address.
        /// The addition takes into account the \e size of the address
        /// space, and the Address will wrap around if necessary.
        /// \param off is the number to add to the offset
        /// \return the new incremented address
        public static Address operator +(Address source, int off)
        {
            if (null == source.@base) {
                throw new BugException();
            }
            if (0 > off) {
                throw new BugException();
            }
            return new Address(source.@base,
                source.@base.wrapOffset(source.offset + (uint)off));
        }

        /// Decrement address by a number of bytes
        /// Subtract an integer value from the offset portion of the
        /// address.  The subtraction takes into account the \e size of
        /// the address space, and the Address will wrap around if necessary.
        /// \param off is the number to subtract from the offset
        /// \return the new decremented address
        public static Address operator -(Address source, int off)
        {
            if (null == source.@base) {
                throw new BugException();
            }
            if (0 > off) {
                throw new BugException();
            }
            return new Address(source.@base,
                source.@base.wrapOffset(source.offset - (uint)off));
        }

        /// Write out an address to stream
        /// friend ostream &operator<<(ostream &s,const Address &addr);

        ///< Determine if \e op2 range contains \b this range
        /// Return \b true if the range starting at \b this extending the given number of bytes
        /// is contained by the second given range.
        /// \param sz is the given number of bytes in \b this range
        /// \param op2 is the start of the second given range
        /// \param sz2 is the number of bytes in the second given range
        /// \return \b true if the second given range contains \b this range
        public bool containedBy(int sz, Address op2, int sz2)
        {
            if (@base != op2.@base) {
                return false;
            }
            if (op2.offset > offset) {
                return false;
            }
            ulong off1 = offset + (uint)(sz - 1);
            ulong off2 = op2.offset + (uint)(sz2 - 1);
            return (off2 >= off1);
        }

        /// Determine if \e op2 is the least significant part of \e this.
        /// Return -1 if (\e op2,\e sz2) is not properly contained in (\e this,\e sz).
        /// If it is contained, return the endian aware offset of (\e op2,\e sz2) 
        /// I.e. if the least significant byte of the \e op2 range falls on the least significant
        /// byte of the \e this range, return 0.  If it intersects the second least significant, return 1, etc.
        /// The -forceleft- toggle causes the check to be made against the left (lowest address) side
        /// of the container, regardless of the endianness.  I.e. it forces a little endian interpretation.
        /// \param sz is the size of \e this range
        /// \param op2 is the address of the second range
        /// \param sz2 is the size of the second range
        /// \param forceleft is \b true if containments is forced to be on the left even for big endian
        /// \return the endian aware offset, or -1
        public int justifiedContain(int sz, Address op2, int sz2, bool forceleft)
        {
            if (@base != op2.@base) {
                return -1;
            }
            if (op2.offset < offset) {
                return -1;
            }
            if (1 > sz) {
                throw new BugException();
            }
            ulong off1 = offset + (uint)(sz - 1);
            if (1 > sz2) {
                throw new BugException();
            }
            ulong off2 = op2.offset + (uint)(sz2 - 1);
            if (off2 > off1) {
                return -1;
            }
            if (null == @base) {
                throw new BugException();
            }
            return (@base.isBigEndian() && !forceleft)
                ? (int)(off1 - off2)
                : (int)(op2.offset - offset);
        }

        /// Determine how \b this address falls in a given address range
        /// If \e this + \e skip falls in the range
        /// \e op to \e op + \e size, then a non-negative integer is
        /// returned indicating where in the interval it falls. I.e.
        /// if \e this + \e skip == \e op, then 0 is returned. Otherwise
        /// -1 is returned.
        /// \param skip is an adjust to \e this address
        /// \param op is the start of the range to check
        /// \param size is the size of the range
        /// \return an integer indicating how overlap occurs
        public int overlap(int skip, Address op, int size)
        {
            ulong dist;

            if (@base != op.@base) {
                // Must be in same address space to overlap
                return -1;
            }
            if (null == @base) {
                throw new BugException();
            }
            if (@base.getType() == spacetype.IPTR_CONSTANT) {
                // Must not be constants
                return -1;
            }
            if (0 > skip) {
                throw new BugException();
            }
            dist = @base.wrapOffset(offset + (ulong)skip - op.offset);
            if (dist >= (ulong)size) {
                // but must fall before op+size
                return -1;
            }
            return (int)dist;
        }

        /// Determine how \b this falls in a possible \e join space address range
        /// This method is equivalent to Address::overlap, but a range in the \e join space can be
        /// considered overlapped with its constituent pieces.
        /// If \e this + \e skip falls in the range, \e op to \e op + \e size, then a non-negative integer is
        /// returned indicating where in the interval it falls. Otherwise -1 is returned.
        /// \param skip is an adjust to \e this address
        /// \param op is the start of the range to check
        /// \param size is the size of the range
        /// \return an integer indicating how overlap occurs
        public int overlapJoin(int skip, Address op, int size)
        {
            return op.getSpace().overlapJoin(op.getOffset(), size, @base, offset, skip);
        }

        /// Does \e this form a contiguous range with \e loaddr
        /// Does the location \e this, \e sz form a contiguous region to \e loaddr, \e losz,
        /// where \e this forms the most significant piece of the logical whole
        /// \param sz is the size of \e this hi region
        /// \param loaddr is the starting address of the low region
        /// \param losz is the size of the low region
        /// \return \b true if the pieces form a contiguous whole
        public bool isContiguous(int sz, Address loaddr, int losz)
        {
            if (@base != loaddr.@base) {
                return false;
            }
            if (null == @base) {
                throw new BugException();
            }
            if (@base.isBigEndian()) {
                if(0 > sz) {
                    throw new BugException();
                }
                ulong nextoff = @base.wrapOffset(offset + (uint)sz);
                if (nextoff == loaddr.offset) {
                    return true;
                }
            }
            else {
                if(0 > losz) {
                    throw new BugException();
                }
                ulong nextoff = @base.wrapOffset(loaddr.offset + (uint)losz);
                if (nextoff == offset) {
                    return true;
                }
            }
            return false;
        }

        /// Is this a \e constant \e value
        /// Determine if this address is from the \e constant \e space.
        /// All constant values are represented as an offset into
        /// the \e constant \e space.
        /// \return \b true if this address represents a constant
        public bool isConstant()
        {
            return (@base.getType() == spacetype.IPTR_CONSTANT);
        }

        /// Make sure there is a backing JoinRecord if \b this is in the \e join space
        /// If \b this is (originally) a \e join address, reevaluate it in terms of its new
        /// \e offset and \e size, changing the space and offset if necessary.
        /// \param size is the new size in bytes of the underlying object
        public void renormalize(int size)
        {
            if (null == @base) {
                throw new BugException();
            }
            if (@base.getType() == spacetype.IPTR_JOIN) {
                @base.getManager().renormalizeJoinAddress(this, size);
            }
        }

        /// Is this a \e join \e value
        /// Determine if this address represents a set of joined memory locations.
        /// \return \b true if this address represents a join
        public bool isJoin()
        {
            return (@base.getType() == spacetype.IPTR_JOIN);
        }

        /// Encode \b this to a stream
        /// Save an \<addr\> element corresponding to this address to a
        /// stream.  The exact format is determined by the address space,
        /// but this generally has a \e space and an \e offset attribute.
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_ADDR);
            if (@base != null) {
                @base.encodeAttributes(encoder, offset);
            }
            encoder.closeElement(ElementId.ELEM_ADDR);
        }

        ///< Encode \b this and a size to a stream
        /// Encode an \<addr> element corresponding to this address to a
        /// stream.  The tag will also include an extra \e size attribute
        /// so that it can describe an entire memory range.
        /// \param encoder is the stream encoder
        /// \param size is the number of bytes in the range
        public void encode(Sla.CORE.Encoder encoder, int size)
        {
            encoder.openElement(ElementId.ELEM_ADDR);
            if (@base != null) {
                @base.encodeAttributes(encoder, offset, size);
            }
            encoder.closeElement(ElementId.ELEM_ADDR);
        }

        /// Restore an address from parsed XML
        /// This is usually used to decode an address from an \b \<addr\>
        /// element, but any element can be used if it has the appropriate attributes
        ///    - \e space indicates the address space of the tag
        ///    - \e offset indicates the offset within the space
        /// or a \e name attribute can be used to recover an address
        /// based on a register name.
        /// \param decoder is the stream decoder
        /// \return the resulting Address
        public static Address decode(Sla.CORE.Decoder decoder)
        {
            VarnodeData var = VarnodeData.decode(decoder);
            return new Address(var.space, var.offset);
        }

        /// Restore an address and size from parsed XML
        /// This is usually used to decode an address from an \b \<addr\>
        /// element, but any element can be used if it has the appropriate attributes
        ///    - \e space indicates the address space of the tag
        ///    - \e offset indicates the offset within the space
        ///    - \e size indicates the size of an address range
        ///
        /// or a \e name attribute can be used to recover an address
        /// and size based on a register name. If a size is recovered
        /// it is stored in \e size reference.
        /// \param decoder is the stream decoder
        /// \param size is the reference to any recovered size
        /// \return the resulting Address
        public static Address decode(Sla.CORE.Decoder decoder, out int size)
        {
            VarnodeData var = VarnodeData.decode(decoder);
            size = (int)var.size;
            return new Address(var.space, var.offset);
        }

        public int CompareTo(Address? other)
        {
            if (object.ReferenceEquals(null, other)) {
                throw new InvalidOperationException();
            }
            throw new NotImplementedException();
        }
    }
}
