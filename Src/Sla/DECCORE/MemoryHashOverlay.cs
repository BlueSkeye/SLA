using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A memory bank that implements reads and writes using a hash table.
    ///
    /// The initial state of the
    /// bank is taken from an \e underlying memory bank or is all zero, if this bank is initialized with
    /// a \b null pointer.  This implementation will not be very efficient for accessing entire pages.
    internal class MemoryHashOverlay : MemoryBank
    {
        /// Underlying memory bank
        private MemoryBank underlie;
        /// How many LSBs are thrown away from address when doing hash table lookup
        private int alignshift;
        /// How many slots to skip after a hashtable collision
        private ulong collideskip;
        /// The hashtable addresses
        private List<ulong> address;
        /// The hashtable values
        private List<ulong> value;

        /// Overridden aligned word insert
        /// Write the value into the hashtable, using \b addr as a key.
        /// \param addr is the aligned address of the word being written
        /// \param val is the value of the word to write
        protected override void insert(ulong addr, ulong val)
        {
            int size = address.size();
            ulong offset = (addr >> alignshift) % (uint)size;
            for (int i = 0; i < size; ++i) {
                if (address[(int)offset] == addr) {
                    // Address has been seen before
                    // Replace old value
                    value[(int)offset] = val;
                    return;
                }
                else if (address[(int)offset] == (ulong)0xBADBEEF) {
                    // Address not seen before
                    // Claim this hash slot
                    address[(int)offset] = addr;
                    // Set value
                    value[(int)offset] = val;
                    return;
                }
                offset = (offset + collideskip) % (uint)size;
            }
            throw new LowlevelError("Memory state hash_table is full");
        }

        /// Overridden aligned word find
        /// First search for an entry in the hashtable using \b addr as a key.  If there is no
        /// entry, forward the query to the underlying memory bank, or return 0 if there is no underlying bank
        /// \param addr is the aligned address of the word to retrieve
        /// \return the retrieved value
        internal override ulong find(ulong addr)
        {
            // Find address in hash-table, or return find from underlying memory
            int size = address.size();
            ulong offset = (addr >> alignshift) % (uint)size;
            for (int i = 0; i < size; ++i)
            {
                if (address[(int)offset] == addr) // Address has been seen before
                    return value[(int)offset];
                else if (address[(int)offset] == 0xBADBEEF) // Address not seen before
                    break;
                offset = (offset + collideskip) % (uint)size;
            }

            // We didn't find the address in the hashtable
            return (underlie == (MemoryBank)null) ? (ulong)0 : underlie.find(addr);
        }

        /// Constructor for hash overlay
        /// A MemoryBank implemented as a hash table needs everything associated with a generic
        /// memory bank, but the constructor also needs to know the size of the hashtable and
        /// the underlying memorybank to forward reads and writes to.
        /// \param spc is the address space associated with the memory bank
        /// \param ws is the number of bytes in the preferred wordsize (must be power of 2)
        /// \param ps is the number of bytes in a page (must be a power of 2)
        /// \param hashsize is the maximum number of entries in the hashtable
        /// \param ul is the underlying memory bank being overlayed
        public MemoryHashOverlay(AddrSpace spc, int ws, int ps, int hashsize, MemoryBank ul)
            : base(spc, (uint)ws, (uint)ps)
        {
            address = new List<ulong>(hashsize);
            for(int index = 0; index < hashsize; index++) {
                address[index] = 0xBADBEEF;
            }
            value = new List<ulong>(hashsize);
            underlie = ul;
            collideskip = 1023;

            uint tmp = (uint)ws - 1;
            alignshift = 0;
            while (tmp != 0) {
                alignshift += 1;
                tmp >>= 1;
            }
        }
    }
}
