using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Memory storage/state for a single AddressSpace
    ///
    /// Class for setting and getting memory values within a space
    /// The basic API is to get/set arrays of byte values via offset within the space.
    /// Helper functions getValue and setValue easily retrieve/store integers
    /// of various sizes from memory, using the endianness encoding specified by the space.
    /// Accesses through the public interface, are automatically broken down into
    /// \b word accesses, through the private insert/find methods, and \b page
    /// accesses through getPage/setPage.  So these are the virtual methods that need
    /// to be overridden in the derived classes.
    internal abstract class MemoryBank
    {
        //friend class MemoryPageOverlay;
        //friend class MemoryHashOverlay;
        /// Number of bytes in an aligned word access
        private uint wordsize;
        /// Number of bytes in an aligned page access
        private uint pagesize;
        /// The address space associated with this memory
        private AddrSpace space;

        /// Insert a word in memory bank at an aligned location
        protected abstract void insert(ulong addr, ulong val);

        /// Retrieve a word from memory bank at an aligned location
        internal abstract ulong find(ulong addr);

        /// Retrieve data from a memory \e page 
        /// This routine only retrieves data from a single \e page in the memory bank. Bytes need not
        /// be retrieved from the exact start of a page, but all bytes must come from \e one page.
        /// A page is a fixed number of bytes, and the address of a page is always aligned based
        /// on that number of bytes.  This routine may be overridden for a page based implementation
        /// of the MemoryBank.  The default implementation retrieves the page as aligned words
        /// using the find method.
        /// \param addr is the \e aligned offset of the desired page
        /// \param res is a pointer to where fetched data should be written
        /// \param skip is the offset \e into \e the \e page to get the bytes from
        /// \param size is the number of bytes to retrieve
        internal virtual void getPage(ulong addr, byte[] res, uint skip, uint size)
        {
            // Default implementation just iterates using find but could be optimized
            ulong ptraddr = addr + skip;
            ulong endaddr = ptraddr + size;
            ulong startalign = ptraddr & ~((ulong)(wordsize - 1));
            ulong endalign = endaddr & ~((ulong)(wordsize - 1));
            if ((endaddr & ((ulong)(wordsize - 1))) != 0)
                endalign += wordsize;

            bool bswap = ((Globals.HOST_ENDIAN == 1) != space.isBigEndian());
            byte* ptr;
            uint resOffset = 0;
            do {
                ulong curval = find(startalign);
                if (bswap) {
                    curval = Globals.byte_swap(curval, wordsize);
                }
                ptr = (byte*)&curval;
                uint sz = wordsize;
                if (startalign < addr) {
                    ptr += (addr - startalign);
                    sz = (uint)(wordsize - (addr - startalign));
                }
                if (startalign + wordsize > endaddr) {
                    sz -= (uint)(startalign + wordsize - endaddr);
                }
                memcpy(res, ptr, sz);
                resOffset += sz;
                startalign += wordsize;
            } while (startalign != endalign);
        }

        /// Write data into a memory page
        /// This routine writes data only to a single \e page of the memory bank. Bytes need not be
        /// written to the exact start of the page, but all bytes must be written to only one page
        /// when using this routine. A page is a
        /// fixed number of bytes, and the address of a page is always aligned based on this size.
        /// This routine may be overridden for a page based implementation of the MemoryBank. The
        /// default implementation writes the page as a sequence of aligned words, using the
        /// insert method.
        /// \param addr is the \e aligned offset of the desired page
        /// \param val is a pointer to the bytes to be written into the page
        /// \param skip is the offset \e into \e the \e page where bytes will be written
        /// \param size is the number of bytes to be written
        protected virtual void setPage(ulong addr, byte[] val, uint skip, uint size)
        {
            // Default implementation just iterates using insert but could be optimized
            ulong ptraddr = addr + skip;
            ulong endaddr = ptraddr + size;
            ulong startalign = ptraddr & ~((ulong)(wordsize - 1));
            ulong endalign = endaddr & ~((ulong)(wordsize - 1));
            if ((endaddr & ((ulong)(wordsize - 1))) != 0)
                endalign += wordsize;

            ulong curval;
            bool bswap = ((Globals.HOST_ENDIAN == 1) != space.isBigEndian());
            byte* ptr;
            do {
                ptr = (byte*)&curval;
                int sz = wordsize;
                if (startalign < addr) {
                    ptr += (addr - startalign);
                    sz = wordsize - (addr - startalign);
                }
                if (startalign + wordsize > endaddr)
                    sz -= (startalign + wordsize - endaddr);
                if (sz != wordsize) {
                    curval = find(startalign); // Part of word is copied from underlying
                    memcpy(ptr, val, sz);    // Rest is taken from -val-
                }
                else
                    curval = *((ulong*)val); // -val- supplies entire word
                if (bswap)
                    curval = Globals.byte_swap(curval, wordsize);
                insert(startalign, curval);
                val += sz;
                startalign += wordsize;
            } while (startalign != endalign);
        }

        /// Generic constructor for a memory bank
        /// A MemoryBank must be associated with a specific address space, have a preferred or natural
        /// \e wordsize and a natural \e pagesize.  Both the \e wordsize and \e pagesize must be a power of 2.
        /// \param spc is the associated address space
        /// \param ws is the number of bytes in the preferred wordsize
        /// \param ps is the number of bytes in a page
        public MemoryBank(AddrSpace spc, uint ws, uint ps)
        {
            space = spc;
            wordsize = ws;
            pagesize = ps;
        }

        ~MemoryBank()
        {
        }

        /// Get the number of bytes in a word for this memory bank
        /// A MemoryBank is instantiated with a \e natural word size. Requests for arbitrary byte ranges
        /// may be broken down into units of this size.
        /// \return the number of bytes in a \e word.
        public uint getWordSize() => wordsize;

        /// Get the number of bytes in a page for this memory bank
        /// A MemoryBank is instantiated with a \e natural page size. Requests for large chunks of data
        /// may be broken down into units of this size.
        /// \return the number of bytes in a \e page.
        public uint getPageSize() => pagesize;

        /// Get the address space associated with this memory bank
        /// A MemoryBank is a contiguous sequence of bytes associated with a particular address space.
        /// \return the AddressSpace associated with this bank.
        public AddrSpace getSpace() => space;

        /// Set the value of a (small) range of bytes
        /// This routine is used to set a single value in the memory bank at an arbitrary address
        /// It takes into account the endianness of the associated address space when encoding the
        /// value as bytes in the bank.  The value is broken up into aligned pieces of \e wordsize and
        /// the actual \b write is performed with the insert routine.  If only parts of aligned words
        /// are written to, then the remaining parts are filled in with the original value, via the
        /// find routine.
        /// \param offset is the start of the byte range to write
        /// \param size is the number of bytes in the range to write
        /// \param val is the value to be written
        public void setValue(ulong offset, uint size, ulong val)
        {
            ulong alignmask = (ulong)(wordsize - 1);
            ulong ind = offset & (~alignmask);
            uint skip = (uint)(offset & alignmask);
            uint size1 = wordsize - skip;
            uint size2;
            uint gap;
            ulong val1;
            ulong val2;

            if (size > size1) {
                // We have spill over
                size2 = size - size1;
                val1 = find(ind);
                val2 = find(ind + wordsize);
                gap = wordsize - size2;
            }
            else {
                if (size == wordsize) {
                    insert(ind, val);
                    return;
                }
                val1 = find(ind);
                val2 = 0;
                gap = size1 - size;
                size1 = size;
                size2 = 0;
            }

            skip = skip * 8;        // Convert from byte skip to bit skip
            gap = gap * 8;      // Convert from byte to bits
            if (space.isBigEndian()) {
                if (size2 == 0) {
                    val1 &= ~(Globals.calc_mask(size1) << gap);
                    val1 |= val << gap;
                    insert(ind, val1);
                }
                else {
                    val1 &= ulong.MaxValue << (int)(8 * size1);
                    val1 |= val >> (int)(8 * size2);
                    insert(ind, val1);
                    val2 &= ulong.MaxValue >> (int)(8 * size2);
                    val2 |= val << (int)gap;
                    insert(ind + wordsize, val2);
                }
            }
            else {
                if (size2 == 0) {
                    val1 &= ~(Globals.calc_mask(size1) << skip);
                    val1 |= val << (int)skip;
                    insert(ind, val1);
                }
                else
                {
                    val1 &= ulong.MaxValue >> (int)(8 * size1);
                    val1 |= val << (int)skip;
                    insert(ind, val1);
                    val2 &= ulong.MaxValue << (int)(8 * size2);
                    val2 |= val >> (int)(8 * size1);
                    insert(ind + wordsize, val2);
                }
            }
        }

        /// Retrieve the value encoded in a (small) range of bytes
        /// This routine gets the value from a range of bytes at an arbitrary address.
        /// It takes into account the endianness of the underlying space when decoding the value.
        /// The value is constructed by making one or more aligned word queries, using the find method.
        /// The desired value may span multiple words and is reconstructed properly.
        /// \param offset is the start of the byte range encoding the value
        /// \param size is the number of bytes in the range
        /// \return the decoded value
        public ulong getValue(ulong offset, int size)
        {
            ulong res;

            ulong alignmask = (ulong)(wordsize - 1);
            ulong ind = offset & (~alignmask);
            int skip = offset & alignmask;
            int size1 = wordsize - skip;
            int size2;
            int gap;
            ulong val1, val2;
            if (size > size1) {
                // We have spill over
                size2 = size - size1;
                val1 = find(ind);
                val2 = find(ind + wordsize);
                gap = wordsize - size2;
            }
            else {
                val1 = find(ind);
                val2 = 0;
                if (size == wordsize)
                    return val1;
                gap = size1 - size;
                size1 = size;
                size2 = 0;
            }

            if (space.isBigEndian()) {
                if (size2 == 0)
                    res = val1 >> (8 * gap);
                else
                    res = (val1 << (8 * size2)) | (val2 >> (8 * gap));
            }
            else {
                if (size2 == 0)
                    res = val1 >> (skip * 8);
                else
                    res = (val1 >> (skip * 8)) | (val2 << (size1 * 8));
            }
            res &= (ulong)Globals.calc_mask((uint)size);
            return res;
        }

        /// Set values of an arbitrary sequence of bytes
        /// This the most general method for writing a sequence of bytes into the memory bank.
        /// There is no restriction on the offset to write to or the number of bytes to be written,
        /// except that the range must be contained in the address space.
        /// \param offset is the start of the byte range to be written
        /// \param size is the number of bytes to write
        /// \param val is a pointer to the sequence of bytes to be written into the bank
        public void setChunk(ulong offset, int size, byte[] val)
        {
            int cursize;
            int count;
            ulong pagemask = (ulong)(pagesize - 1);
            ulong offalign;
            int skip;

            count = 0;
            while (count < size) {
                cursize = pagesize;
                offalign = offset & ~pagemask;
                skip = 0;
                if (offalign != offset) {
                    skip = offset - offalign;
                    cursize -= skip;
                }
                if (size - count < cursize) {
                    cursize = size - count;
                }
                setPage(offalign, val, (uint)skip, cursize);
                count += cursize;
                offset += cursize;
                val += cursize;
            }
        }

        /// Retrieve an arbitrary sequence of bytes
        /// This is the most general method for reading a sequence of bytes from the memory bank.
        /// There is no restriction on the offset or the number of bytes to read, except that the
        /// range must be contained in the address space.
        /// \param offset is the start of the byte range to read
        /// \param size is the number of bytes to read
        /// \param res is a pointer to where the retrieved bytes should be stored
        public void getChunk(ulong offset, int size, byte[] res)
        {
            int cursize, count;
            ulong pagemask = (ulong)(pagesize - 1);
            ulong offalign;
            int skip;

            count = 0;
            while (count < size) {
                cursize = pagesize;
                offalign = offset & ~pagemask;
                skip = 0;
                if (offalign != offset) {
                    skip = offset - offalign;
                    cursize -= skip;
                }
                if (size - count < cursize)
                    cursize = size - count;
                getPage(offalign, res, (uint)skip, cursize);
                count += cursize;
                offset += cursize;
                res += cursize;
            }
        }

        /// Decode bytes to value
        /// This is a static convenience routine for decoding a value from a sequence of bytes depending
        /// on the desired endianness
        /// \param ptr is the pointer to the bytes to decode
        /// \param size is the number of bytes
        /// \param bigendian is \b true if the bytes are encoded in big endian form
        /// \return the decoded value
        public static ulong constructValue(byte[] ptr, int size,bool bigendian)
        {
            ulong res = 0;

            if (bigendian) {
                for (int i = 0; i < size; ++i) {
                    res <<= 8;
                    res += (ulong)ptr[i];
                }
            }
            else {
                for (int i = size - 1; i >= 0; --i) {
                    res <<= 8;
                    res += (ulong)ptr[i];
                }
            }
            return res;
        }

        /// Encode value to bytes
        /// This is a static convenience routine for encoding bytes from a given value, depending on
        /// the desired endianness
        /// \param ptr is a pointer to the location to write the encoded bytes
        /// \param val is the value to be encoded
        /// \param size is the number of bytes to encode
        /// \param bigendian is \b true if a big endian encoding is desired
        public static void deconstructValue(byte[] ptr, ulong val, int size, bool bigendian)
        {
            if (bigendian) {
                for (int i = size - 1; i >= 0; --i) {
                    ptr[i] = (byte)(val & 0xff);
                    val >>= 8;
                }
            }
            else {
                for (int i = 0; i < size; ++i) {
                    ptr[i] = (byte)(val & 0xff);
                    val >>= 8;
                }
            }
        }
    }
}
